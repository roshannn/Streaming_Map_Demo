using GizmoSDK.Gizmo3D;
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Map
{
    public interface IMapViewContext
    {
        void Attach(Camera camera);
        void Detach();
        void SetLodFactor(float lodFactor);
        bool TryGetScreenRay(
            int x,
            int y,
            uint width,
            uint height,
            out Vec3D position,
            out Vec3 direction);
    }

    public sealed class MapViewContext : IMapViewContext
    {
        private readonly object _sync = new object();
        private Camera _camera;
        private float _lodFactor = 1f;

        public void Attach(Camera camera)
        {
            lock (_sync)
                _camera = camera;
        }

        public void Detach()
        {
            lock (_sync)
            {
                _camera = null;
                _lodFactor = 1f;
            }
        }

        public void SetLodFactor(float lodFactor)
        {
            lock (_sync)
                _lodFactor = lodFactor;
        }

        public bool TryGetScreenRay(
            int x,
            int y,
            uint width,
            uint height,
            out Vec3D position,
            out Vec3 direction)
        {
            lock (_sync)
            {
                if (_camera == null || !_camera.IsValid())
                {
                    position = new Vec3D();
                    direction = new Vec3();
                    return false;
                }

                _camera.GetScreenVectors(
                    x,
                    y,
                    width,
                    height,
                    out position,
                    out direction);
                return true;
            }
        }

        internal bool TryGetNativeState(
            out Camera camera,
            out float lodFactor)
        {
            lock (_sync)
            {
                camera = _camera;
                lodFactor = _lodFactor;
                return camera != null && camera.IsValid();
            }
        }
    }
}
