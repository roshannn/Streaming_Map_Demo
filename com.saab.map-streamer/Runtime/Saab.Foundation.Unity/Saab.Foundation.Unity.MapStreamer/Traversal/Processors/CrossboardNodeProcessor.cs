// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class CrossboardNodeProcessor : NodeProcessor<Crossboard>
    {
        public CrossboardNodeProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        public override bool RequiresDefaultNodeHandle => false;

        protected override TraversalResult Process(
            Crossboard node,
            ref TraversalContext context)
        {
            NodeEvents.NotifyCrossboardCreated(
                context.NodeHandle.gameObject,
                context.TraversalStateFlags.HasFlag(TraversalState.Asset));
            return TraversalResult.Handled();
        }
    }
}
