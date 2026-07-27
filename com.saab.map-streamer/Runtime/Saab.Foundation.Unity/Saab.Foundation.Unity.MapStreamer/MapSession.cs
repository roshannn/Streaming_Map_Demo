using System;

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;

using UnityEngine;

using gzCamera = GizmoSDK.Gizmo3D.Camera;

namespace Saab.Foundation.Unity.MapStreamer
{
    public delegate void MapLoadErrorHandler(
        ref string url,
        string errorString,
        SerializeAdapter.AdapterError errorType,
        ref bool retry);

    /// <summary>
    /// Owns the native and Unity objects that belong to the active map.
    /// Platform and loader lifetime remain application-level concerns.
    /// </summary>
    public sealed class MapSession : IDisposable
    {
        private const string MessageSource =
            "Saab.Foundation.Unity.MapStreamer.MapSession";

        private readonly DynamicNodeLoadCoordinator _dynamicNodeLoads;
        private readonly GeometryBuilderRegistry _builderRegistry;
        private readonly IGeometryNodeOperations _geometryOperations;
        private readonly TextureManager _textureManager;
        private readonly MaterialManager _materialManager;
        private readonly SceneTraverser _sceneTraverser;
        private readonly NodeHandlePool _nodeHandlePool;
        private readonly NodeHierarchyUnloader _hierarchyUnloader;
        private readonly NodeBuildCoordinator _buildCoordinator;
        private readonly INodeUpdateRegistry _nodeUpdates;
        private readonly IExternalAssetQueue _externalAssets;

        private Scene _nativeScene;
        private gzCamera _nativeCamera;
        private Context _nativeContext;
        private CullTraverseAction _traverseAction;
        private GameObject _root;
        private bool _subscribed;

        internal MapSession(
            DynamicNodeLoadCoordinator dynamicNodeLoads,
            GeometryBuilderRegistry builderRegistry,
            IGeometryNodeOperations geometryOperations,
            TextureManager textureManager,
            MaterialManager materialManager,
            SceneTraverser sceneTraverser,
            NodeHandlePool nodeHandlePool,
            NodeHierarchyUnloader hierarchyUnloader,
            NodeBuildCoordinator buildCoordinator,
            INodeUpdateRegistry nodeUpdates,
            IExternalAssetQueue externalAssets,
            MapConfig mapConfig)
        {
            _dynamicNodeLoads = dynamicNodeLoads;
            _builderRegistry = builderRegistry;
            _geometryOperations = geometryOperations;
            _textureManager = textureManager;
            _materialManager = materialManager;
            _sceneTraverser = sceneTraverser;
            _nodeHandlePool = nodeHandlePool;
            _hierarchyUnloader = hierarchyUnloader;
            _buildCoordinator = buildCoordinator;
            _nodeUpdates = nodeUpdates;
            _externalAssets = externalAssets;
            MapUrl = mapConfig.MapUrl;
        }

        public string MapUrl { get; private set; }
        public gzCamera NativeCamera => _nativeCamera;
        public Context NativeContext => _nativeContext;
        public CullTraverseAction TraverseAction => _traverseAction;
        public bool IsInitialized => _nativeScene != null;

        public event Action<Node> MapChanged;
        public event MapLoadErrorHandler MapLoadError;

        public void Initialize()
        {
            if (IsInitialized)
                return;

            NodeLock.WaitLockEdit();
            try
            {
                _nativeCamera = new PerspCamera("Map Camera")
                {
                    RoiPosition = true
                };
                MapControl.SystemMap.Camera = _nativeCamera;

                _nativeScene = new Scene("Scene");
                _nativeCamera.Scene = _nativeScene;
                _nativeContext = new Context();
                _traverseAction = new CullTraverseAction();
            }
            finally
            {
                NodeLock.UnLock();
            }

            _dynamicNodeLoads.Subscribe();
            _sceneTraverser.SetActionReceiver(
                _dynamicNodeLoads.ActionReceiver);
            _subscribed = true;
        }

        public bool Load() => Load(MapUrl);

        public bool Load(string mapUrl)
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "The map session must be initialized before loading a map.");

            NodeLock.WaitLockEdit();
            try
            {
                ResetLocked();

                Node node = null;
                while (!string.IsNullOrEmpty(mapUrl))
                {
                    var errorString = string.Empty;
                    var errorType = SerializeAdapter.AdapterError.NO_ERROR;
                    var retry = false;
                    node = DbManager.LoadDB(
                        mapUrl,
                        ref errorString,
                        ref errorType);

                    if (node != null && node.IsValid())
                        break;

                    Message.Send(
                        MessageSource,
                        MessageLevel.WARNING,
                        $"Failed to load map {mapUrl}");
                    MapLoadError?.Invoke(
                        ref mapUrl,
                        errorString,
                        errorType,
                        ref retry);

                    if (!retry)
                        return false;
                }

                MapUrl = mapUrl;
                MapControl.SystemMap.NodeURL = mapUrl;
                MapControl.SystemMap.CurrentMap = node;

                if (node == null)
                    return true;

                _nativeScene.AddNode(node);
                _root = new GameObject("root");
                var sceneRoot = _sceneTraverser.Begin(node);
                if (sceneRoot != null)
                    sceneRoot.transform.SetParent(_root.transform, false);

                // GizmoSDK uses the opposite Z direction to Unity.
                _root.transform.localScale = new Vector3(1, 1, -1);
                MapChanged?.Invoke(node);
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

            _nativeScene?.RemoveAllNodes();
            _textureManager.Clear();
            _materialManager.Clear();
            _sceneTraverser.AssetPolicy.ClearDeferred();
            _buildCoordinator.Clear();
            _nodeUpdates.Clear();
            _externalAssets.Clear();
            MapControl.SystemMap.Reset();
        }

        public void Dispose()
        {
            if (!IsInitialized)
                return;

            Reset();

            NodeLock.WaitLockEdit();
            try
            {
                _nativeCamera.Debug(_nativeContext, false);
                _nativeCamera.Dispose();
                _nativeCamera = null;

                _nativeContext.Dispose();
                _nativeContext = null;

                _nativeScene.Dispose();
                _nativeScene = null;
                _traverseAction = null;
            }
            finally
            {
                NodeLock.UnLock();
            }

            if (_subscribed)
            {
                _dynamicNodeLoads.Unsubscribe();
                _sceneTraverser.SetActionReceiver(null);
                _subscribed = false;
            }
        }
    }
}
