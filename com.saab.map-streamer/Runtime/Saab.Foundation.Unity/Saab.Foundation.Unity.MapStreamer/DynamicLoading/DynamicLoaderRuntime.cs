using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.DynamicLoading
{
    internal sealed class DynamicLoaderRuntime
    {
        private bool _started;
        private bool _running;

        public void Start(byte loaderCount)
        {
            if (_started)
            {
                Resume();
                return;
            }

            DynamicLoader.UsePreCache(true);
            DynamicLoaderManager.SetNumberOfActiveLoaders(loaderCount);
            DynamicLoaderManager.StartManager();
            _started = true;
            _running = true;
        }

        public void Stop()
        {
            if (!_started)
                return;

            if (_running)
                DynamicLoaderManager.StopManager();

            _running = false;
            _started = false;
        }

        public void Pause()
        {
            if (!_started || !_running)
                return;

            DynamicLoaderManager.StopManager();
            _running = false;
        }

        public void Resume()
        {
            if (!_started || _running)
                return;

            DynamicLoaderManager.StartManager();
            _running = true;
        }
    }
}
