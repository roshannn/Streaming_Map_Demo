using System;

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Streaming.Pipeline
{
    internal readonly struct StreamingFrameContext
    {
        public StreamingFrameContext(
            Context nativeContext,
            CullTraverseAction traverseAction,
            in SceneManagerSettings settings,
            Action<bool> notifyPreTraverse,
            Action<double> notifyCameraUpdated)
        {
            NativeContext = nativeContext;
            TraverseAction = traverseAction;
            Settings = settings;
            NotifyPreTraverse = notifyPreTraverse;
            NotifyCameraUpdated = notifyCameraUpdated;
        }

        public Context NativeContext { get; }
        public CullTraverseAction TraverseAction { get; }
        public SceneManagerSettings Settings { get; }
        public Action<bool> NotifyPreTraverse { get; }
        public Action<double> NotifyCameraUpdated { get; }
    }
}
