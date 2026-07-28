using System;
using System.Diagnostics;

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Streaming.Synchronization;
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
        private readonly DynamicNodeLoadCoordinator _dynamicNodeLoads;
        private readonly NodeHandlePool _nodeHandlePool;
        private readonly NodeBuildCoordinator _buildCoordinator;
        private readonly INodeUpdateRegistry _nodeUpdates;
        private readonly RuntimeMapStreamerSettings _settings;
        private readonly CameraControl _cameraControl;
        private readonly IStreamingRuntimeState _runtime;
        private readonly Stopwatch _frameTimer = new Stopwatch();

        private bool _ownsLock;

        public StreamingPipeline(
            IStreamingLock streamingLock,
            DynamicNodeLoadCoordinator dynamicNodeLoads,
            NodeHandlePool nodeHandlePool,
            NodeBuildCoordinator buildCoordinator,
            INodeUpdateRegistry nodeUpdates,
            RuntimeMapStreamerSettings settings,
            CameraControl cameraControl,
            IStreamingRuntimeState runtime)
        {
            _streamingLock = streamingLock;
            _dynamicNodeLoads = dynamicNodeLoads;
            _nodeHandlePool = nodeHandlePool;
            _buildCoordinator = buildCoordinator;
            _nodeUpdates = nodeUpdates;
            _settings = settings;
            _cameraControl = cameraControl;
            _runtime = runtime;
        }

        internal event Action<bool> PreTraverse;
        internal event Action<double> CameraUpdated;

        internal StreamingPipelineState State { get; private set; } =
            StreamingPipelineState.Unlocked;

        public bool ProcessFrame()
        {
            var unityCamera = _cameraControl.Camera;
            if (!_runtime.IsInitialized ||
                unityCamera == null ||
                _runtime.NativeCamera == null ||
                _runtime.NativeContext == null ||
                _runtime.TraverseAction == null)
                return false;

            // A callback may attempt to render recursively. Reject it without
            // entering the cleanup path, which belongs to the active frame.
            if (State != StreamingPipelineState.Unlocked)
            {
                Message.Send(
                    MessageSource,
                    MessageLevel.WARNING,
                    $"Cannot begin a frame while state is {State}.");
                return false;
            }

            _frameTimer.Restart();
            var traversed = false;

            try
            {
                if (!TryBeginEditing())
                    return false;

                ProcessPendingLoads();

                if (!TryBeginRendering())
                    return false;

                if (_dynamicNodeLoads.HasPendingLoads)
                {
                    AbortFrame(
                        StreamingPipelineState.Rendering,
                        "Mismatch in virtual context (loaded/unloaded data)",
                        MessageLevel.FATAL);
                    return false;
                }

                TraverseNativeScene(unityCamera);
                traversed = true;

                if (!TryBeginPostProcessing())
                    return traversed;

                ProcessTraversalResults();
                CompleteFrame();
                return traversed;
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

        private bool TryBeginEditing()
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
                PreTraverse?.Invoke(false);
                return false;
            }

            _ownsLock = true;
            State = StreamingPipelineState.Editing;
            PreTraverse?.Invoke(true);
            return true;
        }

        private void ProcessPendingLoads()
        {
            EnsureState(StreamingPipelineState.Editing);

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

        private void TraverseNativeScene(UnityEngine.Camera unityCamera)
        {
            EnsureState(StreamingPipelineState.Rendering);

            using (ProfilerMarkerCull.Auto())
            {
                var lodFactor = _cameraControl.LodFactor;
                Lod.SetLODFactor(_runtime.NativeContext, lodFactor);
                MapControl.SystemMap.LodFactor = lodFactor;

                var renderTime = GizmoSDK.GizmoBase.Time.SystemSeconds;
                renderTime = _cameraControl.UpdateCamera(renderTime);
                CameraUpdated?.Invoke(renderTime);
                _runtime.NativeContext.CurrentRenderTime = renderTime;

                if (_runtime.NativeCamera is PerspCamera perspectiveCamera)
                {
                    perspectiveCamera.VerticalFOV = unityCamera.fieldOfView;
                    perspectiveCamera.HorizontalFOV =
                        2 * Mathf.Atan(
                            Mathf.Tan(
                                unityCamera.fieldOfView *
                                Mathf.Deg2Rad / 2) *
                            unityCamera.aspect) *
                        Mathf.Rad2Deg;
                    perspectiveCamera.NearClipPlane =
                        unityCamera.nearClipPlane;
                    perspectiveCamera.FarClipPlane =
                        unityCamera.farClipPlane;
                }

                _runtime.NativeCamera.Transform =
                    unityCamera.transform.worldToLocalMatrix
                        .ToZFlippedMatrix4();
                _runtime.NativeCamera.Position =
                    _cameraControl.GlobalPosition;
                _runtime.NativeCamera.Render(
                    _runtime.NativeContext,
                    1000,
                    1000,
                    1000,
                    _runtime.TraverseAction);
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

        private void ProcessTraversalResults()
        {
            EnsureState(StreamingPipelineState.PostProcessing);
            _dynamicNodeLoads.ProcessActivations();
            _nodeHandlePool.ProcessPending(1000);
            _nodeHandlePool.PreAllocate(
                10000,
                TimeSpan.FromMilliseconds(1));

            var remainingBuildTime =
                TimeSpan.FromSeconds(_settings.MaxBuildTime) -
                _frameTimer.Elapsed;
            var minimumBuildTime =
                TimeSpan.FromSeconds(_settings.MinBuildTime);
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
