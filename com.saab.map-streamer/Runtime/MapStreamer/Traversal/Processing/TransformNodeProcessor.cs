// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal.Contracts;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using gzTransform = GizmoSDK.Gizmo3D.Transform;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processing
{
    internal sealed class TransformNodeProcessor :
        NodeProcessor<gzTransform>,
        IRequiresDependency<IHierarchyTraversal>
    {
        private IHierarchyTraversal _hierarchy;

        public TransformNodeProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        public void Inject(IHierarchyTraversal dependency) => _hierarchy = dependency;

        protected override TraversalResult Process(
            gzTransform node,
            ref TraversalContext context)
        {
            var gameObject = context.Node.GameObject;
            NodeTransformApplicator.Apply(node, gameObject.transform);
            _hierarchy.TraverseChildren(node, in context);

            return TraversalResult.Created(context.Node);
        }
    }
}
