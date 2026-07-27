// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class LodNodeProcessor :
        NodeProcessor<Lod>,
        IRequiresDependency<IHierarchyTraversal>
    {
        private IHierarchyTraversal _hierarchy;

        public LodNodeProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        public void Inject(IHierarchyTraversal dependency) => _hierarchy = dependency;

        protected override TraversalResult Process(
            Lod node,
            ref TraversalContext context)
        {
            _hierarchy.TraverseChildren(node, in context, true);
            NodeEvents.NotifyLodCreated(
                context.Node.GameObject,
                context.TraversalStateFlags.HasFlag(TraversalState.Asset));

            return TraversalResult.Created(context.Node);
        }
    }
}
