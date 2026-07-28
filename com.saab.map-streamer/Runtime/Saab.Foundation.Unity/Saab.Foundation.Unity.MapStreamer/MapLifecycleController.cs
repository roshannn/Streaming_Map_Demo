using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Traversal;

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

        private readonly NativeSceneResources _nativeScene;
        private readonly SceneTraverser _sceneTraverser;
        private readonly StreamingContentResetter _contentResetter;
        private readonly MapConfig _mapConfig;

        private GameObject _root;

        public MapLifecycleController(
            NativeSceneResources nativeScene,
            SceneTraverser sceneTraverser,
            StreamingContentResetter contentResetter,
            MapConfig mapConfig)
        {
            _nativeScene = nativeScene;
            _sceneTraverser = sceneTraverser;
            _contentResetter = contentResetter;
            _mapConfig = mapConfig;
        }

        public bool Load(
            MapLoadErrorHandler onLoadError,
            out Node loadedNode)
        {
            var mapUrl = _mapConfig.MapUrl;
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
            _contentResetter.Reset(_root);
            _root = null;
        }
    }
}
