// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class ReferenceNodeOperations : IReferenceNodeOperations
    {
        private readonly SceneTraverser _traverser;
        private readonly AssetTraversalPolicy _assetPolicy;
        private readonly ITraversalNodeFactory _nodes;
        private readonly ITraversalConfiguration _configuration;

        public ReferenceNodeOperations(
            SceneTraverser traverser,
            AssetTraversalPolicy assetPolicy,
            ITraversalNodeFactory nodes,
            ITraversalConfiguration configuration)
        {
            _traverser = traverser;
            _assetPolicy = assetPolicy;
            _nodes = nodes;
            _configuration = configuration;
        }

        public TraversalNode Process(
            RefNode refNode,
            in TraversalContext context)
        {
            var options = _configuration.Options;
            if (_assetPolicy.IsInstancingDisabled(options))
                return default;

            var state = context;

            System.Diagnostics.Debug.Assert(
                !state.TraversalStateFlags.HasFlag(TraversalState.Asset));
            System.Diagnostics.Debug.Assert(
                !state.TraversalStateFlags.HasFlag(TraversalState.AssetInstance));

            if (_assetPolicy.UsesLazyLoading(options) &&
                _assetPolicy.TryTakeDeferred(
                    refNode.ReferenceNodeID,
                    out var deferredTraversal))
            {
                var assetContext = deferredTraversal.TraversalContext;
                var asset = _traverser.Traverse(
                    deferredTraversal.AssetNode,
                    ref assetContext).GameObject;

                asset.transform.SetParent(
                    deferredTraversal.TraversalContext.Node.Transform);
            }

            refNode.AttachNode();
            state.Node =
                _nodes.Create(refNode, PoolObjectFeature.None);
            state.TraversalStateFlags |= TraversalState.AssetInstance;
            _traverser.TraverseChildren(refNode, in state);

            return state.Node;
        }
    }
}
