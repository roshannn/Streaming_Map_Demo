// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class ExternalReferenceNodeProcessor : NodeProcessor<ExtRef>
    {
        public ExternalReferenceNodeProcessor(SceneManager sceneManager) : base(sceneManager) { }

        protected override TraversalResult Process(
            ExtRef node,
            ref TraversalContext context)
        {
            var gameObject = context.NodeHandle.gameObject;
            SceneManager.ProcessExternalReference(node, gameObject);
            return TraversalResult.Created(gameObject);
        }
    }
}
