// Copyright 2021 saab AB

using System.Collections.Generic;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal sealed class AssetTraversalPolicy
    {
        private readonly Dictionary<uint, DeferredAssetTraversal> _deferredTraversals =
            new Dictionary<uint, DeferredAssetTraversal>();

        public TraversalResult? Evaluate(
            Node node,
            SceneManagerOptions options,
            ref TraversalContext context)
        {
            var isAssetTopNode =
                (context.TraversalStateFlags & (TraversalState.Asset | TraversalState.AssetInstance)) ==
                TraversalState.None &&
                node.HasNodeID();

            if (!isAssetTopNode)
                return null;

            if (IsInstancingDisabled(options))
                return TraversalResult.Skipped();

            System.Diagnostics.Debug.Assert(
                !context.TraversalStateFlags.HasFlag(TraversalState.AssetInstance));

            node.CopyMode = (CopyMode)(
                CopyModeNode.SHARE_GEOMETRY |
                CopyModeNode.SHARE_STATE |
                CopyModeNode.SHARE_TEXTURE);

            context.TraversalStateFlags |= TraversalState.Asset;

            if (!UsesLazyLoading(options))
                return null;

            _deferredTraversals.Add(node.NodeID, new DeferredAssetTraversal
            {
                AssetNode = node,
                TraversalContext = context,
            });

            return TraversalResult.Deferred();
        }

        public bool IsInstancingDisabled(SceneManagerOptions options)
        {
            return options.HasFlag(SceneManagerOptions.DisableInstancing);
        }

        public bool UsesLazyLoading(SceneManagerOptions options)
        {
            return options.HasFlag(SceneManagerOptions.LazyLoadAssets);
        }

        public bool TryTakeDeferred(uint nodeId, out DeferredAssetTraversal deferredTraversal)
        {
            if (!_deferredTraversals.TryGetValue(nodeId, out deferredTraversal))
                return false;

            _deferredTraversals.Remove(nodeId);
            return true;
        }

        public void ClearDeferred()
        {
            _deferredTraversals.Clear();
        }
    }
}
