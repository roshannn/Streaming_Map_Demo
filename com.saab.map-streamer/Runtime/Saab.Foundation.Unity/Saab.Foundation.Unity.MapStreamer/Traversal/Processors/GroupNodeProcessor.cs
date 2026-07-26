// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class GroupNodeProcessor :
        NodeProcessor<Group>,
        IRequiresDependency<IHierarchyTraversal>
    {
        private IHierarchyTraversal _hierarchy;

        public GroupNodeProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        public void Inject(IHierarchyTraversal dependency) => _hierarchy = dependency;

        protected override TraversalResult Process(
            Group node,
            ref TraversalContext context)
        {
            _hierarchy.TraverseChildren(node, in context);
            return TraversalResult.Created(context.NodeHandle.gameObject);
        }
    }
}
