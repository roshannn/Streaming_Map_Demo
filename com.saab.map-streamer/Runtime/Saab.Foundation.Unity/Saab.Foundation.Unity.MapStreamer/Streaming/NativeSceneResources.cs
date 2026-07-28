using System;

using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;

namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    /// <summary>
    /// Owns the native objects that make up the streamed render scene.
    /// Callers are responsible for holding the appropriate native node lock
    /// while initializing, changing the scene, or disposing these resources.
    /// </summary>
    internal sealed class NativeSceneResources : IDisposable
    {
        private readonly NativeCameraController _cameraController;
        private Scene _scene;

        public Context Context { get; private set; }
        public bool IsInitialized => _scene != null;

        public NativeSceneResources(NativeCameraController cameraController)
        {
            _cameraController = cameraController;
        }

        public void SetStreamingCamera(IStreamingCamera streamingCamera)
        {
            _cameraController.SetStreamingCamera(streamingCamera);
        }

        public void Initialize()
        {
            if (IsInitialized)
                return;

            var scene = new Scene("Scene");
            var context = new Context();

            _scene = scene;
            Context = context;
            _cameraController.Initialize(scene, MapControl.SystemMap);
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

            _cameraController.Dispose(Context);
            Context.Dispose();
            _scene.Dispose();

            Context = null;
            _scene = null;
        }
    }
}
