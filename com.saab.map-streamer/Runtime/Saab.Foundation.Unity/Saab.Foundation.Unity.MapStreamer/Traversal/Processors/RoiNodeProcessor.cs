// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class RoiNodeProcessor :
        NodeProcessor<RoiNode>,
        IRequiresDependency<IHierarchyTraversal>,
        IRequiresDependency<INodeUpdateRegistry>
    {
        private IHierarchyTraversal _hierarchy;
        private INodeUpdateRegistry _updates;

        public RoiNodeProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        public void Inject(IHierarchyTraversal dependency) => _hierarchy = dependency;
        public void Inject(INodeUpdateRegistry dependency) => _updates = dependency;

        protected override TraversalResult Process(
            RoiNode node,
            ref TraversalContext context)
        {
            _updates.RegisterForUpdate(context.NodeHandle);
            NodeTransformApplicator.Apply(node, context.NodeHandle.transform);
            _hierarchy.TraverseChildren(node, in context);

            return TraversalResult.Created(context.NodeHandle.gameObject);
        }
    }
}
