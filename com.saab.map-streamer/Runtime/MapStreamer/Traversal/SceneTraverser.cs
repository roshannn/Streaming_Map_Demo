// Copyright 2021 saab AB

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using UnityEngine;

using Saab.Foundation.Unity.MapStreamer.ExternalAssets;
using Saab.Foundation.Unity.MapStreamer.Traversal.Contracts;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Foundation.Unity.MapStreamer.Traversal.Operations;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processing;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal sealed class SceneTraverser : IHierarchyTraversal
    {
        private readonly ITraversalConfiguration _configuration;
        private readonly IntersectionTraversalFilter _intersectionFilter;
        private readonly NodeProcessorFactory _processorFactory;
        private readonly HierarchyTraversalHelper _hierarchyHelper;
        private readonly ITraversalNodeFactory _nodeFactory;
        private NodeAction _actionReceiver;

        public SceneTraverser(
            ITraversalConfiguration configuration,
            NodeEvents nodeEvents,
            INodeUpdateRegistry nodeUpdateRegistry,
            IExternalAssetQueue externalAssetQueue,
            ITraversalNodeFactory nodeFactory,
            IGeometryNodeOperations geometryOperations)
        {
            _configuration = configuration;
            _intersectionFilter = new IntersectionTraversalFilter();
            _nodeFactory = nodeFactory;
            AssetPolicy = new AssetTraversalPolicy();
            _hierarchyHelper = new HierarchyTraversalHelper(this);

            var referenceOperations = new ReferenceNodeOperations(
                this,
                AssetPolicy,
                nodeFactory,
                configuration);

            var composer = new NodeProcessorComposer(
                this,
                nodeUpdateRegistry,
                externalAssetQueue,
                referenceOperations,
                geometryOperations);

            _processorFactory = new NodeProcessorFactory(composer, nodeEvents);
        }

        public AssetTraversalPolicy AssetPolicy { get; }

        public GameObject Begin(Node node)
        {
            System.Diagnostics.Debug.Assert(node != null && node.IsValid());

            var context = new TraversalContext
            {
                IntersectMask = node.IntersectMask,
            };

            return Traverse(node, ref context).GameObject;
        }

        public TraversalResult Traverse(Node node, ref TraversalContext context)
        {
            var filterResult =
                _intersectionFilter.Evaluate(
                    node,
                    _configuration.IntersectMask,
                    ref context);

            if (filterResult.HasValue)
                return filterResult.Value;

            var assetResult =
                AssetPolicy.Evaluate(node, _configuration.Options, ref context);

            if (assetResult.HasValue)
                return assetResult.Value;

            var processor = _processorFactory.Resolve(node);

            if (processor != null && !processor.RequiresDefaultTraversalNode)
                return processor.Process(node, ref context);

            context.Node =
                _nodeFactory.Create(node, PoolObjectFeature.None);

            if (node.HasState())
                context.ActiveStateNode = context.Node;

            var gameObject = context.Node.GameObject;

            if (node.CullMask == CullMaskValue.ALL)
                gameObject.SetActive(false);

            if (processor != null)
                return processor.Process(node, ref context);

            return TraversalResult.Created(context.Node);
        }

        public void SetActionReceiver(NodeAction actionReceiver)
        {
            _actionReceiver = actionReceiver;
        }

        public void TraverseChildren(
            Group group,
            in TraversalContext context,
            bool addActionInterfaces = false)
        {
            _hierarchyHelper.TraverseChildren(
                group,
                in context,
                addActionInterfaces,
                _actionReceiver);
        }
    }
}
