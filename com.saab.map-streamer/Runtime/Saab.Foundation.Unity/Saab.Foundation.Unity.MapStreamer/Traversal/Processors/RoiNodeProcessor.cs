// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class RoiNodeProcessor : NodeProcessor<RoiNode>
    {
        public RoiNodeProcessor(SceneManager sceneManager) : base(sceneManager) { }

        protected override TraversalResult Process(
            RoiNode node,
            ref TraversalContext context)
        {
            SceneManager.RegisterNodeForUpdate(context.NodeHandle);
            NodeTransformApplicator.Apply(node, context.NodeHandle.transform);
            SceneManager.TraverseChildren(node, in context);

            return TraversalResult.Created(context.NodeHandle.gameObject);
        }
    }
}
