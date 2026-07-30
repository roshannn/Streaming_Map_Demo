using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal sealed class TraversalConfiguration : ITraversalConfiguration
    {
        private readonly RuntimeMapStreamerSettings _settings;

        public TraversalConfiguration(RuntimeMapStreamerSettings settings)
        {
            _settings = settings;
        }

        public IntersectMaskValue IntersectMask => _settings.IntersectMask;
        public MapStreamerOptions Options => _settings.Options;
    }
}
