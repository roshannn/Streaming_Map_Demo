using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.GizmoAdapter;

namespace Saab.Foundation.Unity.MapStreamer.GizmoIntegration
{
    internal sealed class GizmoSceneCallbacks : IGizmoSceneCallbacks
    {
        public void AttachCamera(Camera camera)
        {
            MapControl.SystemMap.Camera = camera;
        }

        public void DetachCamera()
        {
            MapControl.SystemMap.Camera = null;
        }

        public void SetLodFactor(float lodFactor)
        {
            MapControl.SystemMap.LodFactor = lodFactor;
        }
    }
}
