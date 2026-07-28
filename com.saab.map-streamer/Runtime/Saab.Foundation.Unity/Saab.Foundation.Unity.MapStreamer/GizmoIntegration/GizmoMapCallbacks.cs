using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.GizmoAdapter;
using Saab.Foundation.Unity.MapStreamer.Traversal;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.GizmoIntegration
{
    internal sealed class GizmoMapCallbacks : IGizmoMapCallbacks
    {
        private readonly GizmoStreamingBackend _backend;
        private readonly SceneTraverser _traverser;

        public GizmoMapCallbacks(
            GizmoStreamingBackend backend,
            SceneTraverser traverser)
        {
            _backend = backend;
            _traverser = traverser;
        }

        public GameObject Install(string url, Node node)
        {
            MapControl.SystemMap.NodeURL = url;
            MapControl.SystemMap.CurrentMap = node;
            if (node == null)
                return null;

            var currentMap = MapControl.SystemMap.CurrentMap;
            _backend.AddNode(currentMap);

            var root = new GameObject("root");
            var scene = _traverser.Begin(currentMap);
            if (scene != null)
                scene.transform.SetParent(root.transform, false);

            root.transform.localScale = new Vector3(1, 1, -1);
            return root;
        }
    }
}
