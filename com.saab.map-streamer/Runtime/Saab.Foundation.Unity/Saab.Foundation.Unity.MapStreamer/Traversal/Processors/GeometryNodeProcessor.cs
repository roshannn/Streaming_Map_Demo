// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class GeometryNodeProcessor :
        NodeProcessor<Geometry>,
        IRequiresDependency<IGeometryNodeOperations>
    {
        private IGeometryNodeOperations _operations;

        public GeometryNodeProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        public void Inject(IGeometryNodeOperations dependency) =>
            _operations = dependency;

        public override bool RequiresDefaultNodeHandle => false;

        protected override TraversalResult Process(
            Geometry node,
            ref TraversalContext context)
        {
            return TraversalResult.Created(
                _operations.Process(node, in context));
        }
    }
}
