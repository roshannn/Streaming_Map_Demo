using System;

using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;

using gzCamera = GizmoSDK.Gizmo3D.Camera;

namespace Saab.Foundation.Unity.MapStreamer.Streaming.Native
{
    /// <summary>
    /// Owns the native objects that make up the streamed render scene.
    /// Callers are responsible for holding the appropriate native node lock
    /// while initializing, changing the scene, or disposing these resources.
    /// </summary>
    internal sealed class NativeSceneResources : IDisposable
    {
        private Scene _scene;

        public gzCamera Camera { get; private set; }
        public Context Context { get; private set; }
        public bool IsInitialized => Camera != null;

        public void Initialize()
        {
            if (IsInitialized)
                return;

            var camera = new PerspCamera("Test")
            {
                RoiPosition = true,
            };
            var scene = new Scene("Scene");
            var context = new Context();

            camera.Scene = scene;
            MapControl.SystemMap.Camera = camera;

            Camera = camera;
            _scene = scene;
            Context = context;
        }

        public void AddNode(Node node)
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "Native scene resources must be initialized before adding a node.");

            _scene.AddNode(node);
#if DEBUG
            _scene.Debug();
#endif
        }

        public void ClearScene()
        {
            _scene?.RemoveAllNodes();
        }

        public void Dispose()
        {
            if (!IsInitialized)
                return;

            MapControl.SystemMap.Camera = null;

            Camera.Dispose();
            Context.Dispose();
            _scene.Dispose();

            Camera = null;
            Context = null;
            _scene = null;
        }
    }
}
