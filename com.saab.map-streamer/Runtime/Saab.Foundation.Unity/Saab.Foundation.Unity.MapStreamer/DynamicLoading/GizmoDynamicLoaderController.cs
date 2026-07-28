using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.DynamicLoading
{
    internal sealed class GizmoDynamicLoaderController
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
