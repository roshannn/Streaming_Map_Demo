using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.Configuration;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal interface ITraversalConfiguration
    {
        IntersectMaskValue IntersectMask { get; }
        MapStreamerOptions Options { get; }
        void Update(in MapStreamerRuntimeSettings settings);
    }
}
