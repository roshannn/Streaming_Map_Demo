// Copyright 2021 saab AB

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using UnityEngine;

using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal sealed class SceneTraverser : IHierarchyTraversal
    {
        private readonly SceneManager _sceneManager;
        private readonly IntersectionTraversalFilter _intersectionFilter;
        private readonly NodeProcessorFactory _processorFactory;
        private readonly HierarchyTraversalHelper _hierarchyHelper;
        private readonly INodeHandleFactory _nodeHandleFactory;
        private NodeAction _actionReceiver;

        public SceneTraverser(
            SceneManager sceneManager,
            NodeEvents nodeEvents,
            INodeUpdateRegistry nodeUpdateRegistry,
            IExternalAssetQueue externalAssetQueue,
            INodeHandleFactory nodeHandleFactory,
            IGeometryNodeOperations geometryOperations)
        {
            _sceneManager = sceneManager;
            _intersectionFilter = new IntersectionTraversalFilter();
            _nodeHandleFactory = nodeHandleFactory;
            AssetPolicy = new AssetTraversalPolicy();
            _hierarchyHelper = new HierarchyTraversalHelper(this);

            var referenceOperations = new ReferenceNodeOperations(
                this,
                AssetPolicy,
                nodeHandleFactory,
                () => sceneManager.Settings.Options);

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
                    _sceneManager.Settings.IntersectMask,
                    ref context);

            if (filterResult.HasValue)
                return filterResult.Value;

            var assetResult =
                AssetPolicy.Evaluate(node, _sceneManager.Settings.Options, ref context);

            if (assetResult.HasValue)
                return assetResult.Value;

            var processor = _processorFactory.Resolve(node);

            if (processor != null && !processor.RequiresDefaultNodeHandle)
                return processor.Process(node, ref context);

            context.NodeHandle =
                _nodeHandleFactory.Create(node, PoolObjectFeature.None);

            if (node.HasState())
                context.ActiveStateNode = context.NodeHandle;

            var gameObject = context.NodeHandle.gameObject;

            if (node.CullMask == CullMaskValue.ALL)
                gameObject.SetActive(false);

            if (processor != null)
                return processor.Process(node, ref context);

            return TraversalResult.Created(gameObject);
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
