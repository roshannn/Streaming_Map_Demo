// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal struct TraversalContext
    {
        public NodeHandle NodeHandle;
        public NodeHandle ActiveStateNode;
        public TraversalState TraversalStateFlags;
        public IntersectMaskValue IntersectMask;
    }
}
