// Copyright 2021 saab AB

using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    /// <summary>
    /// Traversal-facing view of a pooled node object.
    /// Keeps traversal code from depending on NodeHandle's mutable lifecycle state.
    /// </summary>
    internal readonly struct TraversalNode
    {
        private readonly NodeHandle _handle;

        public TraversalNode(NodeHandle handle)
        {
            _handle = handle;
        }

        public GameObject GameObject => _handle.gameObject;
        public UnityEngine.Transform Transform => _handle.transform;
        public PoolObjectFeature Feature => _handle.featureKey;
        public bool IsValid => _handle != null;
        internal NodeBuildTarget BuildTarget =>
            new NodeBuildTarget(_handle);

        public void MarkAsAssetInstance()
        {
            _handle.stateFlags |= NodeStateFlags.AssetInstance;
        }

        public void MarkRegisteredInNodeUtils()
        {
            _handle.inNodeUtilsRegistry = true;
        }

        internal void EnableTransformUpdates()
        {
            _handle.updateTransform = true;
            _handle.inNodeUpdateList = true;
        }

    }
}
