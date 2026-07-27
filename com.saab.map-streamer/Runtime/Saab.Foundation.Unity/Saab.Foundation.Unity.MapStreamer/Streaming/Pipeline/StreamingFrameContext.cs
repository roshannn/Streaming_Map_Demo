using System;

using unCamera = UnityEngine.Camera;

namespace Saab.Foundation.Unity.MapStreamer.Streaming.Pipeline
{
    internal readonly struct StreamingFrameContext
    {
        public StreamingFrameContext(
            ISceneManagerCamera sceneCamera,
            unCamera unityCamera,
            Action<bool> notifyPreTraverse,
            Action<double> notifyCameraUpdated)
        {
            SceneCamera = sceneCamera;
            UnityCamera = unityCamera;
            NotifyPreTraverse = notifyPreTraverse;
            NotifyCameraUpdated = notifyCameraUpdated;
        }

        public ISceneManagerCamera SceneCamera { get; }
        public unCamera UnityCamera { get; }
        public Action<bool> NotifyPreTraverse { get; }
        public Action<double> NotifyCameraUpdated { get; }
    }
}
