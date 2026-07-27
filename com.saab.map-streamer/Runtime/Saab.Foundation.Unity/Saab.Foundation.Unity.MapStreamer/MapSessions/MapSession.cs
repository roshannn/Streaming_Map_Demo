using System;

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.Native;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Streaming.Synchronization;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.MapSessions
{
    internal sealed class MapSession
    {
        private const string MessageSource =
            "Saab.Foundation.Unity.MapStreamer.MapSession";

        private readonly DynamicNodeLoadCoordinator _dynamicNodeLoads;
        private readonly GeometryBuilderRegistry _builders;
        private readonly IGeometryNodeOperations _geometryOperations;
        private readonly NodeHierarchyUnloader _hierarchyUnloader;
        private readonly NodeHandlePool _nodeHandlePool;
        private readonly TextureManager _textures;
        private readonly MaterialManager _materials;
        private readonly SceneTraverser _sceneTraverser;
        private readonly NodeBuildCoordinator _builds;
        private readonly INodeUpdateRegistry _nodeUpdates;
        private readonly IExternalAssetQueue _externalAssets;
        private readonly NativeSceneSession _nativeSceneSession;
        private readonly IStreamingLock _streamingLock;

        private GameObject _unityRoot;

        public MapSession(
            MapConfig mapConfig,
            DynamicNodeLoadCoordinator dynamicNodeLoads,
            GeometryBuilderRegistry builders,
            IGeometryNodeOperations geometryOperations,
            NodeHierarchyUnloader hierarchyUnloader,
            NodeHandlePool nodeHandlePool,
            TextureManager textures,
            MaterialManager materials,
            SceneTraverser sceneTraverser,
            NodeBuildCoordinator builds,
            INodeUpdateRegistry nodeUpdates,
            IExternalAssetQueue externalAssets,
            NativeSceneSession nativeSceneSession,
            IStreamingLock streamingLock)
        {
            MapUrl = mapConfig.MapUrl;
            _dynamicNodeLoads = dynamicNodeLoads;
            _builders = builders;
            _geometryOperations = geometryOperations;
            _hierarchyUnloader = hierarchyUnloader;
            _nodeHandlePool = nodeHandlePool;
            _textures = textures;
            _materials = materials;
            _sceneTraverser = sceneTraverser;
            _builds = builds;
            _nodeUpdates = nodeUpdates;
            _externalAssets = externalAssets;
            _nativeSceneSession = nativeSceneSession;
            _streamingLock = streamingLock;
        }

        public string MapUrl { get; private set; }

        public MapLoadResult Load(
            string mapUrl,
            MapLoadErrorHandler onLoadError)
        {
            var nativeScene = _nativeSceneSession.Scene;
            using (_streamingLock.AcquireEdit())
            {
                ResetLocked(nativeScene);

                Node node = null;
                while (!string.IsNullOrEmpty(mapUrl))
                {
                    var error = string.Empty;
                    var errorType = SerializeAdapter.AdapterError.NO_ERROR;
                    var retry = false;
                    node = DbManager.LoadDB(
                        mapUrl,
                        ref error,
                        ref errorType);

                    if (node != null && node.IsValid())
                        break;

                    Message.Send(
                        MessageSource,
                        MessageLevel.WARNING,
                        $"Failed to load map {mapUrl}");
                    onLoadError?.Invoke(
                        ref mapUrl,
                        error,
                        errorType,
                        ref retry);

                    if (retry)
                        continue;

                    return MapLoadResult.Failed();
                }

                MapUrl = mapUrl;
                MapControl.SystemMap.NodeURL = mapUrl;
                MapControl.SystemMap.CurrentMap = node;

                if (node != null)
                    AttachMap(nativeScene, node);

                return MapLoadResult.Loaded(node);
            }
        }

        public void Reset()
        {
            var nativeScene = _nativeSceneSession.Scene;
            using (_streamingLock.AcquireEdit())
                ResetLocked(nativeScene);
        }

        private void AttachMap(Scene nativeScene, Node node)
        {
            if (nativeScene == null)
                throw new InvalidOperationException(
                    "The native scene must be initialized before loading a map.");

            nativeScene.AddNode(node);
#if DEBUG
            nativeScene.Debug();
#endif

            _unityRoot = new GameObject("root");
            var sceneRoot = _sceneTraverser.Begin(node);
            if (sceneRoot != null)
                sceneRoot.transform.SetParent(_unityRoot.transform, false);

            // GizmoSDK uses the opposite Z-axis convention from Unity.
            _unityRoot.transform.localScale = new Vector3(1, 1, -1);
        }

        private void ResetLocked(Scene nativeScene)
        {
            _dynamicNodeLoads.Reset();
            _builders.Reset();
            _geometryOperations.Reset();

            if (_unityRoot)
            {
                _hierarchyUnloader.Unload(_unityRoot.transform);
                _nodeHandlePool.QueueFree(_unityRoot.transform);
                _nodeHandlePool.ProcessPending(int.MaxValue);
                _unityRoot = null;
            }

            nativeScene?.RemoveAllNodes();
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
