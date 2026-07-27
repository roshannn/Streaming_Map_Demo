// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal struct TraversalContext
    {
        public TraversalNode Node;
        public TraversalNode ActiveStateNode;
        public TraversalState TraversalStateFlags;
        public IntersectMaskValue IntersectMask;
    }
}
