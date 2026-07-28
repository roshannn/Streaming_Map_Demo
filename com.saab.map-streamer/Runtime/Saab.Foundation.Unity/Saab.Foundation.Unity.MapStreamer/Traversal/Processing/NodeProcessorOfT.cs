// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processing
{
    internal abstract class NodeProcessor<TNode> : NodeProcessor
        where TNode : Node
    {
        protected NodeProcessor(NodeEvents nodeEvents)
        {
            NodeEvents = nodeEvents;
        }

        protected NodeEvents NodeEvents { get; }

        public sealed override TraversalResult Process(
            Node node,
            ref TraversalContext context)
        {
            return Process((TNode)node, ref context);
        }

        protected abstract TraversalResult Process(
            TNode node,
            ref TraversalContext context);
    }
}
