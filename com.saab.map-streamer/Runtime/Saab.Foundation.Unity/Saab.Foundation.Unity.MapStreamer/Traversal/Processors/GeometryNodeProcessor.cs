// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class GeometryNodeProcessor : NodeProcessor<Geometry>
    {
        public GeometryNodeProcessor(SceneManager sceneManager) : base(sceneManager) { }

        public override bool RequiresDefaultNodeHandle => false;

        protected override TraversalResult Process(
            Geometry node,
            ref TraversalContext context)
        {
            return TraversalResult.Created(
                SceneManager.ProcessGeometry(node, in context));
        }
    }
}
