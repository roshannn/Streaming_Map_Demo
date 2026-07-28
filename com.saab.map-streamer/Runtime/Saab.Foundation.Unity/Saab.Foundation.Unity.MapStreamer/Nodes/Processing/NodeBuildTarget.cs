// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Building.Builders;

namespace Saab.Foundation.Unity.MapStreamer.Nodes.Processing
{
    /// <summary>
    /// Builder-facing view of a pooled node. Keeps build execution and native
    /// handle access out of the traversal model.
    /// </summary>
    internal readonly struct NodeBuildTarget
    {
        private readonly NodeHandle _handle;

        public NodeBuildTarget(NodeHandle handle)
        {
            _handle = handle;
        }

        public byte Version => _handle.version;
        public bool HasNativeNode => _handle.node != null;
        public bool IsValid => _handle != null;

        public bool Build(
            INodeBuilder builder,
            NodeBuildTarget activeState)
        {
            var activeHandle =
                activeState.IsValid ? activeState._handle : null;
            return builder.Build(_handle, activeHandle);
        }

        public void RegisterAssetPrefab(
            Geometry geometry,
            AssetInstanceBuilder assetInstances)
        {
            assetInstances.AddAssetPrefab(geometry, _handle);
        }
    }
}
