using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.Configuration;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal sealed class TraversalConfiguration : ITraversalConfiguration
    {
        public IntersectMaskValue IntersectMask { get; private set; }
        public MapStreamerOptions Options { get; private set; }

        public void Update(in MapStreamerRuntimeSettings settings)
        {
            IntersectMask = settings.IntersectMask;
            Options = settings.Options;
        }
    }
}
