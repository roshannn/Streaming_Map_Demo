using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.GizmoAdapter;

namespace Saab.Foundation.Unity.MapStreamer.GizmoIntegration
{
    internal sealed class GizmoSceneCallbacks : IGizmoSceneCallbacks
    {
        private readonly IMapViewContext _viewContext;

        public GizmoSceneCallbacks(IMapViewContext viewContext)
        {
            _viewContext = viewContext;
        }

        public void AttachCamera(Camera camera)
        {
            _viewContext.Attach(camera);
        }

        public void DetachCamera()
        {
            _viewContext.Detach();
        }

        public void SetLodFactor(float lodFactor)
        {
            _viewContext.SetLodFactor(lodFactor);
        }
    }
}
