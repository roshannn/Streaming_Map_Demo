using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
    internal sealed class StreamingContentResetter
    {
        private readonly DynamicNodeLoadCoordinator _dynamicNodeLoads;
        private readonly GeometryBuilderRegistry _builders;
        private readonly IGeometryNodeOperations _geometry;
        private readonly NodeHierarchyUnloader _hierarchy;
        private readonly NodeHandlePool _nodePool;
        private readonly NativeSceneResources _nativeScene;
        private readonly TextureManager _textures;
        private readonly MaterialManager _materials;
        private readonly SceneTraverser _sceneTraverser;
        private readonly NodeBuildCoordinator _builds;
        private readonly INodeUpdateRegistry _nodeUpdates;
        private readonly IExternalAssetResetter _externalAssets;

        public StreamingContentResetter(
            DynamicNodeLoadCoordinator dynamicNodeLoads,
            GeometryBuilderRegistry builders,
            IGeometryNodeOperations geometry,
            NodeHierarchyUnloader hierarchy,
            NodeHandlePool nodePool,
            NativeSceneResources nativeScene,
            TextureManager textures,
            MaterialManager materials,
            SceneTraverser sceneTraverser,
            NodeBuildCoordinator builds,
            INodeUpdateRegistry nodeUpdates,
            IExternalAssetResetter externalAssets)
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
            MapControl.SystemMap.Reset();
        }
    }
}
