// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using gzTransform = GizmoSDK.Gizmo3D.Transform;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class TransformNodeProcessor : NodeProcessor<gzTransform>
    {
        public TransformNodeProcessor(SceneManager sceneManager) : base(sceneManager) { }

        protected override TraversalResult Process(
            gzTransform node,
            ref TraversalContext context)
        {
            var gameObject = context.NodeHandle.gameObject;
            NodeTransformApplicator.Apply(node, gameObject.transform);
            SceneManager.TraverseChildren(node, in context);

            return TraversalResult.Created(gameObject);
        }
    }
}
