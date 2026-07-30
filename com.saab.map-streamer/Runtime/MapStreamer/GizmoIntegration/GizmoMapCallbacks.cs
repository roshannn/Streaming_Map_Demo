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
        private readonly IMapSession _mapSession;

        public GizmoMapCallbacks(
            GizmoStreamingBackend backend,
            SceneTraverser traverser,
            IMapSession mapSession)
        {
            _backend = backend;
            _traverser = traverser;
            _mapSession = mapSession;
        }

        public GameObject Install(string url, Node node)
        {
            if (node == null)
                return null;

            var installedRoot = _mapSession.Install(url, node);
            _backend.AddNode(installedRoot);

            var root = new GameObject("root");
            var scene = _traverser.Begin(installedRoot);
            if (scene != null)
                scene.transform.SetParent(root.transform, false);

            root.transform.localScale = new Vector3(1, 1, -1);
            return root;
        }
    }
}
