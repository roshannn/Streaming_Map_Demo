// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class RoiProcessor : NodeProcessor<Roi>
    {
        public RoiProcessor(SceneManager sceneManager) : base(sceneManager) { }

        protected override TraversalResult Process(
            Roi node,
            ref TraversalContext context)
        {
            SceneManager.RegisterNodeForUpdate(context.NodeHandle);
            NodeTransformApplicator.Apply(node, context.NodeHandle.transform);
            SceneManager.TraverseChildren(node, in context, true);

            return TraversalResult.Created(context.NodeHandle.gameObject);
        }
    }
}
