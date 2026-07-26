// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class RoiProcessor :
        NodeProcessor<Roi>,
        IRequiresDependency<IHierarchyTraversal>,
        IRequiresDependency<INodeUpdateRegistry>
    {
        private IHierarchyTraversal _hierarchy;
        private INodeUpdateRegistry _updates;

        public RoiProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        public void Inject(IHierarchyTraversal dependency) => _hierarchy = dependency;
        public void Inject(INodeUpdateRegistry dependency) => _updates = dependency;

        protected override TraversalResult Process(
            Roi node,
            ref TraversalContext context)
        {
            _updates.RegisterForUpdate(context.NodeHandle);
            NodeTransformApplicator.Apply(node, context.NodeHandle.transform);
            _hierarchy.TraverseChildren(node, in context, true);

            return TraversalResult.Created(context.NodeHandle.gameObject);
        }
    }
}
