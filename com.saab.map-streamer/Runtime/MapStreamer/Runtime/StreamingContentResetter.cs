using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.Building.Coordination;
using Saab.Foundation.Unity.MapStreamer.ExternalAssets;
using Saab.Foundation.Unity.MapStreamer.Nodes.Pooling;
using Saab.Foundation.Unity.MapStreamer.Nodes.Processing;
using Saab.Foundation.Unity.MapStreamer.GizmoAdapter;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Contracts;
using Saab.Foundation.Unity.MapStreamer.Traversal.Operations;
using Saab.Foundation.Unity.MapStreamer.Streaming;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Runtime
{
    internal sealed class StreamingContentResetter : IStreamingContentResetter
    {
        private readonly IDynamicLoadPump _dynamicNodeLoads;
        private readonly GeometryBuilderRegistry _builders;
        private readonly IGeometryNodeOperations _geometry;
        private readonly NodeHierarchyUnloader _hierarchy;
        private readonly NodeHandlePool _nodePool;
        private readonly GizmoStreamingBackend _nativeScene;
        private readonly TextureManager _textures;
        private readonly MaterialManager _materials;
        private readonly SceneTraverser _sceneTraverser;
        private readonly NodeBuildCoordinator _builds;
        private readonly INodeUpdateRegistry _nodeUpdates;
        private readonly IExternalAssetResetter _externalAssets;
        private readonly IMapSession _mapSession;

        public StreamingContentResetter(
            IDynamicLoadPump dynamicNodeLoads,
            GeometryBuilderRegistry builders,
            IGeometryNodeOperations geometry,
            NodeHierarchyUnloader hierarchy,
            NodeHandlePool nodePool,
            GizmoStreamingBackend nativeScene,
            TextureManager textures,
            MaterialManager materials,
            SceneTraverser sceneTraverser,
            NodeBuildCoordinator builds,
            INodeUpdateRegistry nodeUpdates,
            IExternalAssetResetter externalAssets,
            IMapSession mapSession)
        {
            _dynamicNodeLoads = dynamicNodeLoads;
            _builders = builders;
            _geometry = geometry;
            _hierarchy = hierarchy;
            _nodePool = nodePool;
            _nativeScene = nativeScene;
            _textures = textures;
            _materials = materials;
            _sceneTraverser = sceneTraverser;
            _builds = builds;
            _nodeUpdates = nodeUpdates;
            _externalAssets = externalAssets;
            _mapSession = mapSession;
        }

        public void Reset(GameObject root)
        {
            _dynamicNodeLoads.Reset();
            _builders.Reset();
            _geometry.Reset();

            if (root)
            {
                _hierarchy.Unload(root.transform);
                _nodePool.QueueFree(root.transform);
                _nodePool.ProcessPending(int.MaxValue);
            }

            _nativeScene.ClearScene();
            _textures.Clear();
            _materials.Clear();
            _sceneTraverser.AssetPolicy.ClearDeferred();
            _builds.Clear();
            _nodeUpdates.Clear();
            _externalAssets.Clear();
            _mapSession.Reset();
        }
    }
}
