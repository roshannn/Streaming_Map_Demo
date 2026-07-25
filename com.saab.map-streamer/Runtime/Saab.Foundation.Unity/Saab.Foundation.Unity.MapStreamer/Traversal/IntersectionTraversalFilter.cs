// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal sealed class IntersectionTraversalFilter
    {
        public TraversalResult? Evaluate(
            Node node,
            IntersectMaskValue sceneMask,
            ref TraversalContext context)
        {
            var nodeMask = node.IntersectMask;

            // Asset resource nodes use NOTHING, but descendants still need the inherited
            // mask so the correct geometry builder can be selected.
            if (nodeMask != IntersectMaskValue.NOTHING)
                context.IntersectMask &= nodeMask;

            if ((context.IntersectMask & sceneMask) == IntersectMaskValue.NOTHING)
                return TraversalResult.Filtered();

            return null;
        }
    }
}
