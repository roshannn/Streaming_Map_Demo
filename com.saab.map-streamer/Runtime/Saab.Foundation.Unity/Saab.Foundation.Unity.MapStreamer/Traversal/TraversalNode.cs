// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
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
        internal byte Version => _handle.version;
        internal bool HasNativeNode => _handle.node != null;

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

        internal bool Build(
            INodeBuilder builder,
            TraversalNode activeState)
        {
            var activeHandle =
                activeState.IsValid ? activeState._handle : null;

            if (!builder.Build(_handle, activeHandle))
                return false;

            _handle.builder = builder;
            return true;
        }

        internal void RegisterAssetPrefab(
            Geometry geometry,
            AssetInstanceBuilder assetInstances)
        {
            assetInstances.AddAssetPrefab(geometry, _handle);
        }
    }
}
