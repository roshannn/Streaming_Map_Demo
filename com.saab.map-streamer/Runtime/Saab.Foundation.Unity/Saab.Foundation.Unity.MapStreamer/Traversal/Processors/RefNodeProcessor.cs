// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class RefNodeProcessor : NodeProcessor<RefNode>
    {
        public RefNodeProcessor(SceneManager sceneManager) : base(sceneManager) { }

        public override bool RequiresDefaultNodeHandle => false;

        protected override TraversalResult Process(
            RefNode node,
            ref TraversalContext context)
        {
            var gameObject = SceneManager.ProcessRefNode(node, in context);
            return gameObject != null
                ? TraversalResult.Created(gameObject)
                : TraversalResult.Skipped();
        }
    }
}
