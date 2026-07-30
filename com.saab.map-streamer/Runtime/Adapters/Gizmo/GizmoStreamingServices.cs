using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.Streaming;

namespace Saab.Foundation.Unity.MapStreamer.GizmoAdapter
{
    public sealed class GizmoStreamingLock : IStreamingLock
    {
        public bool IsRenderLock => NodeLock.IsLockedRender();
        public bool IsOwnedByCurrentThread => NodeLock.IsLockedByMe();
        public void AcquireEdit() => NodeLock.WaitLockEdit();

        public bool TryAcquireEdit(uint timeoutMilliseconds) =>
            NodeLock.TryLockEdit(timeoutMilliseconds);

        public bool ChangeToRender() => NodeLock.ChangeToRenderLock();
        public bool ChangeToEdit() => NodeLock.ChangeToEditLock();
        public void Release() => NodeLock.UnLock();
    }

    public sealed class GizmoStreamingClock : IStreamingClock
    {
        public double SystemSeconds => GizmoSDK.GizmoBase.Time.SystemSeconds;
    }

    public sealed class GizmoStreamingLog : IStreamingLog
    {
        private const string Source =
            "Saab.Foundation.Unity.MapStreamer.StreamingPipeline";

        public void Write(StreamingLogLevel level, string message)
        {
            Message.Send(Source, ToMessageLevel(level), message);
        }

        private static MessageLevel ToMessageLevel(StreamingLogLevel level)
        {
            switch (level)
            {
                case StreamingLogLevel.Debug:
                    return MessageLevel.DEBUG;
                case StreamingLogLevel.Warning:
                    return MessageLevel.WARNING;
                case StreamingLogLevel.Fatal:
                    return MessageLevel.FATAL;
                default:
                    return MessageLevel.WARNING;
            }
        }
    }

    public sealed class GizmoDynamicLoaderRuntime : IDynamicLoaderRuntime
    {
        private bool _running;

        public void Start(byte loaderCount)
        {
            DynamicLoader.UsePreCache(true);
            DynamicLoaderManager.SetNumberOfActiveLoaders(loaderCount);

            if (_running)
                return;

            DynamicLoaderManager.StartManager();
            _running = true;
        }

        public void Stop()
        {
            if (!_running)
                return;

            DynamicLoaderManager.StopManager();
            _running = false;
        }
    }
}
