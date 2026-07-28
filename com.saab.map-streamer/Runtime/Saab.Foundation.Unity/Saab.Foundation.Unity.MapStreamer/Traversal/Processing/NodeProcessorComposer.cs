// Copyright 2021 saab AB

using Saab.Foundation.Unity.MapStreamer.ExternalAssets;
using Saab.Foundation.Unity.MapStreamer.Traversal.Contracts;
using Saab.Foundation.Unity.MapStreamer.Traversal.Operations;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processing
{
    internal sealed class NodeProcessorComposer
    {
        private readonly IHierarchyTraversal _hierarchy;
        private readonly INodeUpdateRegistry _updates;
        private readonly IExternalAssetQueue _assetQueue;
        private readonly IReferenceNodeOperations _references;
        private readonly IGeometryNodeOperations _geometry;

        public NodeProcessorComposer(
            IHierarchyTraversal hierarchy,
            INodeUpdateRegistry updates,
            IExternalAssetQueue assetQueue,
            IReferenceNodeOperations references,
            IGeometryNodeOperations geometry)
        {
            _hierarchy = hierarchy;
            _updates = updates;
            _assetQueue = assetQueue;
            _references = references;
            _geometry = geometry;
        }

        public TProcessor Compose<TProcessor>(TProcessor processor)
            where TProcessor : NodeProcessor
        {
            Inject(processor, _hierarchy);
            Inject(processor, _updates);
            Inject(processor, _assetQueue);
            Inject(processor, _references);
            Inject(processor, _geometry);
            return processor;
        }

        private static void Inject<TDependency>(
            NodeProcessor processor,
            TDependency dependency)
        {
            if (processor is IRequiresDependency<TDependency> consumer)
                consumer.Inject(dependency);
        }
    }
}
