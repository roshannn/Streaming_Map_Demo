using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal sealed class TraversalConfiguration : ITraversalConfiguration
    {
        public IntersectMaskValue IntersectMask { get; private set; }
        public SceneManagerOptions Options { get; private set; }

        public void Update(in SceneManagerSettings settings)
        {
            IntersectMask = settings.IntersectMask;
            Options = settings.Options;
        }
    }
}
