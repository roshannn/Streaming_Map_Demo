// Copyright 2021 saab AB

using System;
using GizmoSDK.Gizmo3D;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class ReferenceNodeOperations : IReferenceNodeOperations
    {
        private readonly SceneTraverser _traverser;
        private readonly AssetTraversalPolicy _assetPolicy;
        private readonly INodeHandleFactory _nodeHandles;
        private readonly Func<SceneManagerOptions> _options;

        public ReferenceNodeOperations(
            SceneTraverser traverser,
            AssetTraversalPolicy assetPolicy,
            INodeHandleFactory nodeHandles,
            Func<SceneManagerOptions> options)
        {
            _traverser = traverser;
            _assetPolicy = assetPolicy;
            _nodeHandles = nodeHandles;
            _options = options;
        }

        public GameObject Process(RefNode refNode, in TraversalContext context)
        {
            var options = _options();
            if (_assetPolicy.IsInstancingDisabled(options))
                return null;

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
                    deferredTraversal.TraversalContext.NodeHandle.transform);
            }

            refNode.AttachNode();
            state.NodeHandle =
                _nodeHandles.Create(refNode, PoolObjectFeature.None);
            state.TraversalStateFlags |= TraversalState.AssetInstance;
            _traverser.TraverseChildren(refNode, in state);

            return state.NodeHandle.gameObject;
        }
    }
}
