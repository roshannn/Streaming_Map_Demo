using System;
using System.Diagnostics;

namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    public sealed class StreamingPipeline
    {
        private const uint EditLockTimeoutMilliseconds = 30;

        private readonly IStreamingLock _streamingLock;
        private readonly IDynamicLoadPump _dynamicLoads;
        private readonly INodePoolMaintenance _nodePool;
        private readonly IBuildScheduler _builds;
        private readonly INodeUpdateProcessor _nodeUpdates;
        private readonly IStreamingFrameSource _frameSource;
        private readonly IStreamingClock _clock;
        private readonly IStreamingBackend _backend;
        private readonly IStreamingLog _log;
        private readonly IStreamingBudget _budget;
        private readonly IStreamingFrameCompletionSink _frameCompletion;
        private readonly Stopwatch _frameTimer = new Stopwatch();

        private bool _ownsLock;

        public StreamingPipeline(
            IStreamingLock streamingLock,
            IDynamicLoadPump dynamicLoads,
            INodePoolMaintenance nodePool,
            IBuildScheduler builds,
            INodeUpdateProcessor nodeUpdates,
            IStreamingFrameSource frameSource,
            IStreamingClock clock,
            IStreamingBackend backend,
            IStreamingLog log,
            IStreamingBudget budget,
            IStreamingFrameCompletionSink frameCompletion)
        {
            _streamingLock = streamingLock;
            _dynamicLoads = dynamicLoads;
            _nodePool = nodePool;
            _builds = builds;
            _nodeUpdates = nodeUpdates;
            _frameSource = frameSource;
            _clock = clock;
            _backend = backend;
            _log = log;
            _budget = budget;
            _frameCompletion = frameCompletion;
        }

        public event Action<bool> PreTraverse;
        public event Action<double> CameraUpdated;

        public StreamingPipelineState State { get; private set; } =
            StreamingPipelineState.Unlocked;

        public bool ProcessFrame()
        {
            if (!_backend.IsInitialized || !_frameSource.IsAvailable)
                return false;

            if (State != StreamingPipelineState.Unlocked)
            {
                _log.Write(
                    StreamingLogLevel.Warning,
                    $"Cannot begin a frame while state is {State}.");
                return false;
            }

            _frameTimer.Restart();
            var traversed = false;

            try
            {
                if (!TryBeginEditing())
                    return false;

                _dynamicLoads.ProcessLoads();

                if (!TryBeginRendering())
                    return false;

                if (_dynamicLoads.HasPendingLoads)
                {
                    AbortFrame(
                        StreamingPipelineState.Rendering,
                        "Mismatch in virtual context (loaded/unloaded data)",
                        StreamingLogLevel.Fatal);
                    return false;
                }

                var renderTime = _clock.SystemSeconds;
                if (!_frameSource.TryCreateFrame(renderTime, out var frame))
                    return false;

                CameraUpdated?.Invoke(renderTime);
                _backend.Render(in frame);
                traversed = true;

                if (!TryBeginPostProcessing())
                    return traversed;

                ProcessTraversalResults();
                CompleteFrame(frame.RenderTime);
                return traversed;
            }
            catch (Exception exception)
            {
                AbortFrame(
                    State,
                    $"Frame processing failed: {exception}",
                    StreamingLogLevel.Warning);
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
                _log.Write(
                    StreamingLogLevel.Debug,
                    $"Lock contention detected; frame skipped [state={State}].");
                PreTraverse?.Invoke(false);
                return false;
            }

            _ownsLock = true;
            State = StreamingPipelineState.Editing;
            PreTraverse?.Invoke(true);
            return true;
        }

        private bool TryBeginRendering()
        {
            if (State != StreamingPipelineState.Editing)
                return RejectTransition(StreamingPipelineState.Rendering);

            if (!_streamingLock.ChangeToRender() || !_streamingLock.IsRenderLock)
            {
                AbortFrame(
                    StreamingPipelineState.Editing,
                    "Failed to acquire render lock",
                    StreamingLogLevel.Debug);
                return false;
            }

            State = StreamingPipelineState.Rendering;
            return true;
        }

        private bool TryBeginPostProcessing()
        {
            if (State != StreamingPipelineState.Rendering)
                return RejectTransition(StreamingPipelineState.PostProcessing);

            if (!_streamingLock.ChangeToEdit() ||
                !_streamingLock.IsOwnedByCurrentThread)
            {
                AbortFrame(
                    StreamingPipelineState.Rendering,
                    "Failed to reacquire edit lock",
                    StreamingLogLevel.Debug);
                return false;
            }

            State = StreamingPipelineState.PostProcessing;
            return true;
        }

        private void ProcessTraversalResults()
        {
            EnsureState(StreamingPipelineState.PostProcessing);
            _dynamicLoads.ProcessActivations();
            _nodePool.ProcessPending(1000);
            _nodePool.PreAllocate(10000, TimeSpan.FromMilliseconds(1));

            var remaining =
                TimeSpan.FromSeconds(_budget.MaximumBuildTime) -
                _frameTimer.Elapsed;
            var minimum = TimeSpan.FromSeconds(_budget.MinimumBuildTime);
            if (remaining < minimum)
                remaining = minimum;

            _builds.Process(remaining);
        }

        private void CompleteFrame(double renderTime)
        {
            EnsureState(StreamingPipelineState.PostProcessing);
            ReleaseLockIfOwned();
            State = StreamingPipelineState.Unlocked;

            _nodeUpdates.UpdateNodes();

            var completion = new StreamingFrameCompletionContext(
                renderTime,
                _frameTimer.Elapsed);
            _frameCompletion.OnFrameCompleted(in completion);
        }

        private void AbortFrame(
            StreamingPipelineState failedState,
            string reason,
            StreamingLogLevel level)
        {
            _log.Write(
                level,
                $"{reason} [state={failedState}, next={StreamingPipelineState.Aborted}]");
            State = StreamingPipelineState.Aborted;
        }

        private bool RejectTransition(StreamingPipelineState requestedState)
        {
            AbortFrame(
                State,
                $"Invalid streaming pipeline transition to {requestedState}",
                StreamingLogLevel.Warning);
            return false;
        }

        private void EnsureState(StreamingPipelineState expectedState)
        {
            if (State != expectedState)
                throw new InvalidOperationException(
                    $"Streaming pipeline expected state {expectedState}, but was {State}.");
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
