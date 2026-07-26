// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Utility.Unity.NodeUtils;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class DynamicLoaderNodeProcessor : NodeProcessor<DynamicLoader>
    {
        public DynamicLoaderNodeProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        protected override TraversalResult Process(
            DynamicLoader node,
            ref TraversalContext context)
        {
            System.Diagnostics.Debug.Assert(
                !NodeUtils.HasGameObjectsUnsafe(node.GetNativeReference()));

            NodeUtils.AddGameObjectReferenceUnsafe(
                node.GetNativeReference(),
                context.NodeHandle.gameObject);

            context.NodeHandle.inNodeUtilsRegistry = true;
            NodeEvents.NotifyLoaderCreated(context.NodeHandle.gameObject, false);

            return TraversalResult.Created(context.NodeHandle.gameObject);
        }
    }
}
