using System;

using GizmoSDK.Gizmo3D;

using gzCamera = GizmoSDK.Gizmo3D.Camera;
using unCamera = UnityEngine.Camera;

namespace Saab.Foundation.Unity.MapStreamer.Streaming.Pipeline
{
    internal readonly struct StreamingFrameContext
    {
        public StreamingFrameContext(
            ISceneManagerCamera sceneCamera,
            unCamera unityCamera,
            gzCamera nativeCamera,
            Context nativeContext,
            CullTraverseAction traverseAction,
            in SceneManagerSettings settings,
            Action<bool> notifyPreTraverse,
            Action<double> notifyCameraUpdated)
        {
            SceneCamera = sceneCamera;
            UnityCamera = unityCamera;
            NativeCamera = nativeCamera;
            NativeContext = nativeContext;
            TraverseAction = traverseAction;
            Settings = settings;
            NotifyPreTraverse = notifyPreTraverse;
            NotifyCameraUpdated = notifyCameraUpdated;
        }

        public ISceneManagerCamera SceneCamera { get; }
        public unCamera UnityCamera { get; }
        public gzCamera NativeCamera { get; }
        public Context NativeContext { get; }
        public CullTraverseAction TraverseAction { get; }
        public SceneManagerSettings Settings { get; }
        public Action<bool> NotifyPreTraverse { get; }
        public Action<double> NotifyCameraUpdated { get; }
    }
}
