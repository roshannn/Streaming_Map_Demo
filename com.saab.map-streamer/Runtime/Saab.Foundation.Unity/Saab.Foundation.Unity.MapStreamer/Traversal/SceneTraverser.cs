// Copyright 2021 saab AB

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal sealed class SceneTraverser
    {
        private readonly SceneManager _sceneManager;
        private readonly IntersectionTraversalFilter _intersectionFilter;
        private readonly NodeProcessorFactory _processorFactory;
        private readonly HierarchyTraversalHelper _hierarchyHelper;

        public SceneTraverser(SceneManager sceneManager)
        {
            _sceneManager = sceneManager;
            _intersectionFilter = new IntersectionTraversalFilter();
            AssetPolicy = new AssetTraversalPolicy();
            _processorFactory = new NodeProcessorFactory(sceneManager);
            _hierarchyHelper = new HierarchyTraversalHelper(this);
        }

        public AssetTraversalPolicy AssetPolicy { get; }

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
                _sceneManager.CreateNodeHandle(node, PoolObjectFeature.None);

            if (node.HasState())
                context.ActiveStateNode = context.NodeHandle;

            var gameObject = context.NodeHandle.gameObject;

            if (node.CullMask == CullMaskValue.ALL)
                gameObject.SetActive(false);

            if (processor != null)
                return processor.Process(node, ref context);

            return TraversalResult.Created(gameObject);
        }

        public void TraverseChildren(
            Group group,
            in TraversalContext context,
            bool addActionInterfaces,
            NodeAction actionReceiver)
        {
            _hierarchyHelper.TraverseChildren(
                group,
                in context,
                addActionInterfaces,
                actionReceiver);
        }
    }
}
