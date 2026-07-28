// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Building.Builders;
using Saab.Foundation.Unity.MapStreamer.Building.Coordination;
using Saab.Foundation.Unity.MapStreamer.Traversal.Contracts;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Operations
{
    internal sealed class GeometryNodeOperations : IGeometryNodeOperations
    {
        private readonly GeometryBuilderRegistry _builders;
        private readonly NodeBuildCoordinator _builds;
        private readonly ITraversalNodeFactory _nodes;
        private readonly NodeEvents _nodeEvents;
        private readonly AssetInstanceBuilder _assetInstances =
            new AssetInstanceBuilder();

        public GeometryNodeOperations(
            GeometryBuilderRegistry builders,
            NodeBuildCoordinator builds,
            ITraversalNodeFactory nodes,
            NodeEvents nodeEvents)
        {
            _builders = builders;
            _builds = builds;
            _nodes = nodes;
            _nodeEvents = nodeEvents;
        }

        public TraversalNode Process(
            Geometry geometry,
            in TraversalContext context)
        {
            var state = context;
            var isAssetInstance =
                (context.TraversalStateFlags & TraversalState.AssetInstance) ==
                TraversalState.AssetInstance;

            if (isAssetInstance)
            {
                state.Node =
                    _nodes.Create(geometry, PoolObjectFeature.StaticMesh);
                state.Node.MarkAsAssetInstance();
                _builds.Build(_assetInstances, in state);
            }
            else
            {
                var builder = _builders.Resolve(geometry, in state);
                state.Node = _nodes.Create(
                    geometry,
                    builder?.Feature ?? PoolObjectFeature.None);

                if (builder != null)
                {
                    if (geometry.HasState())
                        state.ActiveStateNode = state.Node;

                    _builds.Build(builder, in state);
                }

                if (state.TraversalStateFlags.HasFlag(TraversalState.Asset))
                    _builds.RegisterAssetPrefab(
                        _assetInstances,
                        geometry,
                        state.Node);
            }

            var isAsset =
                state.TraversalStateFlags.HasFlag(TraversalState.Asset);

            switch (state.Node.Feature)
            {
                case PoolObjectFeature.Terrain:
                    _nodeEvents.NotifyTerrainCreated(
                        state.Node.GameObject,
                        isAsset);
                    break;
                case PoolObjectFeature.StaticMesh:
                    _nodeEvents.NotifyGeometryCreated(
                        state.Node.GameObject,
                        isAsset);
                    break;
            }

            return state.Node;
        }

        public void Reset()
        {
            _assetInstances.Reset();
        }
    }
}
