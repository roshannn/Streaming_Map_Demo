// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class ExternalReferenceNodeProcessor :
        NodeProcessor<ExtRef>,
        IRequiresDependency<IExternalAssetQueue>
    {
        private IExternalAssetQueue _assetQueue;

        public ExternalReferenceNodeProcessor(NodeEvents nodeEvents) : base(nodeEvents) { }

        public void Inject(IExternalAssetQueue dependency) => _assetQueue = dependency;

        protected override TraversalResult Process(
            ExtRef node,
            ref TraversalContext context)
        {
            var gameObject = context.NodeHandle.gameObject;
            _assetQueue.Enqueue(gameObject, node.ResourceURL, node.ObjectID);
            return TraversalResult.Created(gameObject);
        }
    }
}
