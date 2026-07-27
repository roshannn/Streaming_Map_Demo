using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Streaming.Synchronization
{
    internal interface IStreamingLock
    {
        bool TryAcquireEdit(uint timeoutMilliseconds);
        bool ChangeToRender();
        bool ChangeToEdit();
        bool IsRenderLock { get; }
        bool IsOwnedByCurrentThread { get; }
        void Release();
    }

    internal sealed class StreamingLock : IStreamingLock
    {
        public bool IsRenderLock => NodeLock.IsLockedRender();
        public bool IsOwnedByCurrentThread => NodeLock.IsLockedByMe();

        public bool TryAcquireEdit(uint timeoutMilliseconds) =>
            NodeLock.TryLockEdit(timeoutMilliseconds);

        public bool ChangeToRender() => NodeLock.ChangeToRenderLock();

        public bool ChangeToEdit() => NodeLock.ChangeToEditLock();

        public void Release() => NodeLock.UnLock();
    }
}
