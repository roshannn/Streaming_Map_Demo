// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal.Operations;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processing
{
    internal sealed class RefNodeProcessor :
        NodeProcessor<RefNode>,
        IRequiresDependency<IReferenceNodeOperations>
    {
        private IReferenceNodeOperations _operations;

        public RefNodeProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        public void Inject(IReferenceNodeOperations dependency) =>
            _operations = dependency;

        public override bool RequiresDefaultTraversalNode => false;

        protected override TraversalResult Process(
            RefNode node,
            ref TraversalContext context)
        {
            var traversalNode = _operations.Process(node, in context);
            return traversalNode.IsValid
                ? TraversalResult.Created(traversalNode)
                : TraversalResult.Skipped();
        }
    }
}
