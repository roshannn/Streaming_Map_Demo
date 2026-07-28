using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
    public delegate void MapLoadErrorHandler(
        ref string url,
        string error,
        SerializeAdapter.AdapterError errorType,
        ref bool retry);

    internal sealed class MapLifecycleController
    {
        private const string MessageSource =
            "Saab.Foundation.Unity.MapStreamer.MapLifecycleController";

        private readonly DynamicNodeLoadCoordinator _dynamicNodeLoads;
        private readonly GeometryBuilderRegistry _builderRegistry;
        private readonly IGeometryNodeOperations _geometryOperations;
        private readonly NodeHierarchyUnloader _hierarchyUnloader;
        private readonly NodeHandlePool _nodeHandlePool;
        private readonly NativeSceneResources _nativeScene;
        private readonly TextureManager _textureManager;
        private readonly MaterialManager _materialManager;
        private readonly SceneTraverser _sceneTraverser;
        private readonly NodeBuildCoordinator _buildCoordinator;
        private readonly INodeUpdateRegistry _nodeUpdates;
        private readonly IExternalAssetQueue _externalAssets;

        private GameObject _root;

        public MapLifecycleController(
            DynamicNodeLoadCoordinator dynamicNodeLoads,
            GeometryBuilderRegistry builderRegistry,
            IGeometryNodeOperations geometryOperations,
            NodeHierarchyUnloader hierarchyUnloader,
            NodeHandlePool nodeHandlePool,
            NativeSceneResources nativeScene,
            TextureManager textureManager,
            MaterialManager materialManager,
            SceneTraverser sceneTraverser,
            NodeBuildCoordinator buildCoordinator,
            INodeUpdateRegistry nodeUpdates,
            IExternalAssetQueue externalAssets)
        {
            _dynamicNodeLoads = dynamicNodeLoads;
            _builderRegistry = builderRegistry;
            _geometryOperations = geometryOperations;
            _hierarchyUnloader = hierarchyUnloader;
            _nodeHandlePool = nodeHandlePool;
            _nativeScene = nativeScene;
            _textureManager = textureManager;
            _materialManager = materialManager;
            _sceneTraverser = sceneTraverser;
            _buildCoordinator = buildCoordinator;
            _nodeUpdates = nodeUpdates;
            _externalAssets = externalAssets;
        }

        public bool Load(
            string configuredUrl,
            MapLoadErrorHandler onLoadError,
            out Node loadedNode)
        {
            var mapUrl = configuredUrl;
            loadedNode = null;

            NodeLock.WaitLockEdit();
            try
            {
                if (!TryLoadNode(ref mapUrl, onLoadError, out loadedNode))
                    return false;

                ResetLocked();
                InstallLocked(mapUrl, loadedNode);
                return true;
            }
            finally
            {
                NodeLock.UnLock();
            }
        }

        public void Reset()
        {
            NodeLock.WaitLockEdit();
            try
            {
                ResetLocked();
            }
            finally
            {
                NodeLock.UnLock();
            }
        }

        private static bool TryLoadNode(
            ref string mapUrl,
            MapLoadErrorHandler onLoadError,
            out Node node)
        {
            node = null;

            while (!string.IsNullOrEmpty(mapUrl))
            {
                var error = string.Empty;
                var errorType = SerializeAdapter.AdapterError.NO_ERROR;
                var retry = false;
                node = DbManager.LoadDB(mapUrl, ref error, ref errorType);

                if (node != null && node.IsValid())
                    return true;

                Message.Send(
                    MessageSource,
                    MessageLevel.WARNING,
                    $"Failed to load map {mapUrl}");
                onLoadError?.Invoke(
                    ref mapUrl,
                    error,
                    errorType,
                    ref retry);

                if (!retry)
                    return false;
            }

            return true;
        }

        private void InstallLocked(string mapUrl, Node node)
        {
            MapControl.SystemMap.NodeURL = mapUrl;
            MapControl.SystemMap.CurrentMap = node;

            if (node == null)
                return;

            var currentMap = MapControl.SystemMap.CurrentMap;
            _nativeScene.AddNode(currentMap);

            _root = new GameObject("root");
            var scene = _sceneTraverser.Begin(currentMap);
            if (scene != null)
                scene.transform.SetParent(_root.transform, false);

            _root.transform.localScale = new Vector3(1, 1, -1);
        }

        private void ResetLocked()
        {
            _dynamicNodeLoads.Reset();
            _builderRegistry.Reset();
            _geometryOperations.Reset();

            if (_root)
            {
                _hierarchyUnloader.Unload(_root.transform);
                _nodeHandlePool.QueueFree(_root.transform);
                _nodeHandlePool.ProcessPending(int.MaxValue);
                _root = null;
            }

            _nativeScene.ClearScene();
            _textureManager.Clear();
            _materialManager.Clear();
            _sceneTraverser.AssetPolicy.ClearDeferred();
            _buildCoordinator.Clear();
            _nodeUpdates.Clear();
            _externalAssets.Clear();
            MapControl.SystemMap.Reset();
        }
    }
}
