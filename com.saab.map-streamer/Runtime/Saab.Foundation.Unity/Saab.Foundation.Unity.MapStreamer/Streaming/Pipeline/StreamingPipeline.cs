using System;
using System.Diagnostics;

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Streaming.Synchronization;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;
using Saab.Unity.Extensions;

using UnityEngine;

using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;

namespace Saab.Foundation.Unity.MapStreamer.Streaming.Pipeline
{
    internal sealed class StreamingPipeline
    {
        private const string MessageSource =
            "Saab.Foundation.Unity.MapStreamer.StreamingPipeline";
        private const uint EditLockTimeoutMilliseconds = 30;

        private static readonly ProfilerMarker ProfilerMarkerCull =
            new ProfilerMarker(ProfilerCategory.Render, "SM-Cull");
        private static readonly ProfilerMarker ProfilerMarkerTraverse =
            new ProfilerMarker(ProfilerCategory.Render, "SM-Traverse");

        private readonly IStreamingLock _streamingLock;
        private readonly ITraversalConfiguration _traversalConfiguration;
        private readonly DynamicNodeLoadCoordinator _dynamicNodeLoads;
        private readonly NodeHandlePool _nodeHandlePool;
        private readonly NodeBuildCoordinator _buildCoordinator;
        private readonly INodeUpdateRegistry _nodeUpdates;
        private readonly Stopwatch _frameTimer = new Stopwatch();

        private bool _ownsLock;

        public StreamingPipeline(
            IStreamingLock streamingLock,
            ITraversalConfiguration traversalConfiguration,
            DynamicNodeLoadCoordinator dynamicNodeLoads,
            NodeHandlePool nodeHandlePool,
            NodeBuildCoordinator buildCoordinator,
            INodeUpdateRegistry nodeUpdates)
        {
            _streamingLock = streamingLock;
            _traversalConfiguration = traversalConfiguration;
            _dynamicNodeLoads = dynamicNodeLoads;
            _nodeHandlePool = nodeHandlePool;
            _buildCoordinator = buildCoordinator;
            _nodeUpdates = nodeUpdates;
        }

        internal StreamingPipelineState State { get; private set; } =
            StreamingPipelineState.Unlocked;

        public void ProcessFrame(in StreamingFrameContext context)
        {
            if (context.UnityCamera == null || context.NativeCamera == null)
                return;

            // A callback may attempt to render recursively. Reject it without
            // entering the cleanup path, which belongs to the active frame.
            if (State != StreamingPipelineState.Unlocked)
            {
                Message.Send(
                    MessageSource,
                    MessageLevel.WARNING,
                    $"Cannot begin a frame while state is {State}.");
                return;
            }

            _frameTimer.Restart();

            try
            {
                if (!TryBeginEditing(context))
                    return;

                ProcessPendingLoads(context.Settings);

                if (!TryBeginRendering())
                    return;

                if (_dynamicNodeLoads.HasPendingLoads)
                {
                    AbortFrame(
                        StreamingPipelineState.Rendering,
                        "Mismatch in virtual context (loaded/unloaded data)",
                        MessageLevel.FATAL);
                    return;
                }

                TraverseNativeScene(context);

                if (!TryBeginPostProcessing())
                    return;

                ProcessTraversalResults(context.Settings);
                CompleteFrame();
            }
            catch (Exception exception)
            {
                AbortFrame(
                    State,
                    $"Frame processing failed: {exception}",
                    MessageLevel.WARNING);
                throw;
            }
            finally
            {
                try
                {
                    ReleaseLockIfOwned();
                }
                finally
                {
                    State = StreamingPipelineState.Unlocked;
                }
            }
        }

        private bool TryBeginEditing(in StreamingFrameContext context)
        {
            if (State != StreamingPipelineState.Unlocked)
                return RejectTransition(StreamingPipelineState.Editing);

            if (!_streamingLock.TryAcquireEdit(EditLockTimeoutMilliseconds))
            {
                Message.Send(
                    MessageSource,
                    MessageLevel.DEBUG,
                    "Lock contention detected! NodeLock::TryLockEdit() FRAME LOST " +
                    $"[state={State}]");
                context.SceneCamera.PreTraverse(false);
                context.NotifyPreTraverse?.Invoke(false);
                return false;
            }

            _ownsLock = true;
            State = StreamingPipelineState.Editing;
            context.SceneCamera.PreTraverse(true);
            context.NotifyPreTraverse?.Invoke(true);
            return true;
        }

        private void ProcessPendingLoads(in SceneManagerSettings settings)
        {
            EnsureState(StreamingPipelineState.Editing);
            _traversalConfiguration.Update(settings);

            using (ProfilerMarkerTraverse.Auto())
                _dynamicNodeLoads.ProcessLoads();
        }

        private bool TryBeginRendering()
        {
            if (State != StreamingPipelineState.Editing)
                return RejectTransition(StreamingPipelineState.Rendering);

            if (!_streamingLock.ChangeToRender())
            {
                AbortFrame(
                    StreamingPipelineState.Editing,
                    "Failed to change into RenderLock",
                    MessageLevel.DEBUG);
                return false;
            }

            if (!_streamingLock.IsRenderLock)
            {
                AbortFrame(
                    StreamingPipelineState.Editing,
                    "Render lock transition completed without owning RenderLock",
                    MessageLevel.DEBUG);
                return false;
            }

            State = StreamingPipelineState.Rendering;
            return true;
        }

        private void TraverseNativeScene(in StreamingFrameContext context)
        {
            EnsureState(StreamingPipelineState.Rendering);

            using (ProfilerMarkerCull.Auto())
            {
                var lodFactor = context.SceneCamera.LodFactor;
                Lod.SetLODFactor(context.NativeContext, lodFactor);
                MapControl.SystemMap.LodFactor = lodFactor;

                var renderTime = GizmoSDK.GizmoBase.Time.SystemSeconds;
                renderTime = context.SceneCamera.UpdateCamera(renderTime);
                context.NotifyCameraUpdated?.Invoke(renderTime);
                context.NativeContext.CurrentRenderTime = renderTime;

                if (context.NativeCamera is PerspCamera perspectiveCamera)
                {
                    perspectiveCamera.VerticalFOV = context.UnityCamera.fieldOfView;
                    perspectiveCamera.HorizontalFOV =
                        2 * Mathf.Atan(
                            Mathf.Tan(
                                context.UnityCamera.fieldOfView *
                                Mathf.Deg2Rad / 2) *
                            context.UnityCamera.aspect) *
                        Mathf.Rad2Deg;
                    perspectiveCamera.NearClipPlane =
                        context.UnityCamera.nearClipPlane;
                    perspectiveCamera.FarClipPlane =
                        context.UnityCamera.farClipPlane;
                }

                context.NativeCamera.Transform =
                    context.SceneCamera.NativeWorldToLocalMatrix
                        .ToZFlippedMatrix4();
                context.NativeCamera.Position =
                    context.SceneCamera.GlobalPosition;
                context.NativeCamera.Render(
                    context.NativeContext,
                    1000,
                    1000,
                    1000,
                    context.TraverseAction);
            }
        }

        private bool TryBeginPostProcessing()
        {
            if (State != StreamingPipelineState.Rendering)
                return RejectTransition(StreamingPipelineState.PostProcessing);

            if (!_streamingLock.ChangeToEdit())
            {
                AbortFrame(
                    StreamingPipelineState.Rendering,
                    "Failed to change into EditLock",
                    MessageLevel.DEBUG);
                return false;
            }

            if (!_streamingLock.IsOwnedByCurrentThread)
            {
                AbortFrame(
                    StreamingPipelineState.Rendering,
                    "Edit lock transition completed without lock ownership",
                    MessageLevel.DEBUG);
                return false;
            }

            State = StreamingPipelineState.PostProcessing;
            return true;
        }

        private void ProcessTraversalResults(
            in SceneManagerSettings settings)
        {
            EnsureState(StreamingPipelineState.PostProcessing);
            _dynamicNodeLoads.ProcessActivations();
            _nodeHandlePool.ProcessPending(1000);
            _nodeHandlePool.PreAllocate(
                10000,
                TimeSpan.FromMilliseconds(1));

            var remainingBuildTime =
                TimeSpan.FromSeconds(settings.MaxBuildTime) -
                _frameTimer.Elapsed;
            var minimumBuildTime =
                TimeSpan.FromSeconds(settings.MinBuildTime);
            if (remainingBuildTime < minimumBuildTime)
                remainingBuildTime = minimumBuildTime;

            _buildCoordinator.Process(remainingBuildTime);
        }

        private void CompleteFrame()
        {
            EnsureState(StreamingPipelineState.PostProcessing);
            ReleaseLockIfOwned();
            State = StreamingPipelineState.Unlocked;
            _nodeUpdates.UpdateNodes();
        }

        private void AbortFrame(
            StreamingPipelineState failedState,
            string reason,
            MessageLevel level)
        {
            Message.Send(
                MessageSource,
                level,
                $"{reason} [state={failedState}, next={StreamingPipelineState.Aborted}]");
            State = StreamingPipelineState.Aborted;
        }

        private bool RejectTransition(StreamingPipelineState requestedState)
        {
            AbortFrame(
                State,
                $"Invalid streaming pipeline transition to {requestedState}",
                MessageLevel.WARNING);
            return false;
        }

        private void EnsureState(StreamingPipelineState expectedState)
        {
            if (State != expectedState)
                throw new InvalidOperationException(
                    $"Streaming pipeline expected state {expectedState}, " +
                    $"but was {State}.");
        }

        private void ReleaseLockIfOwned()
        {
            if (!_ownsLock)
                return;

            _ownsLock = false;
            _streamingLock.Release();
        }
    }
}
