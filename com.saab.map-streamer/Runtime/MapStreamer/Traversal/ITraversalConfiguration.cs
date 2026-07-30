using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal interface ITraversalConfiguration
    {
        IntersectMaskValue IntersectMask { get; }
        MapStreamerOptions Options { get; }
    }
}
