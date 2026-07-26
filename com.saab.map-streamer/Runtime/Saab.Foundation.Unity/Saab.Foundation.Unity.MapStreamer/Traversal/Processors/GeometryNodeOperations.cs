// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class GeometryNodeOperations : IGeometryNodeOperations
    {
        private readonly GeometryBuilderRegistry _builders;
        private readonly NodeBuildScheduler _scheduler;
        private readonly INodeHandleFactory _nodeHandles;
        private readonly NodeEvents _nodeEvents;
        private readonly AssetInstanceBuilder _assetInstances =
            new AssetInstanceBuilder();

        public GeometryNodeOperations(
            GeometryBuilderRegistry builders,
            NodeBuildScheduler scheduler,
            INodeHandleFactory nodeHandles,
            NodeEvents nodeEvents)
        {
            _builders = builders;
            _scheduler = scheduler;
            _nodeHandles = nodeHandles;
            _nodeEvents = nodeEvents;
        }

        public GameObject Process(Geometry geometry, in TraversalContext context)
        {
            var state = context;
            var isAssetInstance =
                (context.TraversalStateFlags & TraversalState.AssetInstance) ==
                TraversalState.AssetInstance;

            if (isAssetInstance)
            {
                state.NodeHandle =
                    _nodeHandles.Create(geometry, PoolObjectFeature.StaticMesh);
                state.NodeHandle.stateFlags |= NodeStateFlags.AssetInstance;
                _scheduler.Build(_assetInstances, in state);
            }
            else
            {
                var builder = _builders.Resolve(geometry, in state);
                state.NodeHandle = _nodeHandles.Create(
                    geometry,
                    builder?.Feature ?? PoolObjectFeature.None);

                if (builder != null)
                {
                    if (geometry.HasState())
                        state.ActiveStateNode = state.NodeHandle;

                    _scheduler.Build(builder, in state);
                }

                if (state.TraversalStateFlags.HasFlag(TraversalState.Asset))
                    _assetInstances.AddAssetPrefab(geometry, state.NodeHandle);
            }

            var isAsset =
                state.TraversalStateFlags.HasFlag(TraversalState.Asset);

            switch (state.NodeHandle.featureKey)
            {
                case PoolObjectFeature.Terrain:
                    _nodeEvents.NotifyTerrainCreated(
                        state.NodeHandle.gameObject,
                        isAsset);
                    break;
                case PoolObjectFeature.StaticMesh:
                    _nodeEvents.NotifyGeometryCreated(
                        state.NodeHandle.gameObject,
                        isAsset);
                    break;
            }

            return state.NodeHandle.gameObject;
        }

        public void Reset()
        {
            _assetInstances.Reset();
        }
    }
}
