// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class CrossboardNodeProcessor : NodeProcessor<Crossboard>
    {
        public CrossboardNodeProcessor(SceneManager sceneManager) : base(sceneManager) { }

        public override bool RequiresDefaultNodeHandle => false;

        protected override TraversalResult Process(
            Crossboard node,
            ref TraversalContext context)
        {
            SceneManager.ProcessCrossboard(node, in context);
            return TraversalResult.Handled();
        }
    }
}
