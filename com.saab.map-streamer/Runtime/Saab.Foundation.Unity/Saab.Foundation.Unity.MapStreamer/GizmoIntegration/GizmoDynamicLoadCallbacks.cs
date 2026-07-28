using System;
using System.Collections.Generic;

using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.GizmoAdapter;
using Saab.Foundation.Unity.MapStreamer.Nodes.Pooling;
using Saab.Foundation.Unity.MapStreamer.Nodes.Processing;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Utility.Unity.NodeUtils;

using UnityEngine;
using UnityTransform = UnityEngine.Transform;

namespace Saab.Foundation.Unity.MapStreamer.GizmoIntegration
{
    internal sealed class GizmoDynamicLoadCallbacks :
        IGizmoDynamicLoadCallbacks,
        IStreamedHierarchyRelease
    {
        private readonly SceneTraverser _traverser;
        private readonly NodeHierarchyUnloader _hierarchy;
        private readonly NodeHandlePool _nodePool;

        public GizmoDynamicLoadCallbacks(
            SceneTraverser traverser,
            NodeHierarchyUnloader hierarchy,
            NodeHandlePool nodePool)
        {
            _traverser = traverser;
            _hierarchy = hierarchy;
            _nodePool = nodePool;
        }

        public void SetActionReceiver(NodeAction receiver) =>
            _traverser.SetActionReceiver(receiver);

        public GameObject Traverse(Node node) => _traverser.Begin(node);

        public UnityTransform FindLoaderAnchor(IntPtr nativeReference) =>
            NodeUtils.FindFirstGameObjectTransformUnsafe(nativeReference);

        public bool TryFindGameObjects(
            IntPtr nativeReference,
            out IReadOnlyList<GameObject> gameObjects)
        {
            if (NodeUtils.FindGameObjectsUnsafe(
                nativeReference,
                out List<GameObject> found))
            {
                gameObjects = found;
                return true;
            }

            gameObjects = Array.Empty<GameObject>();
            return false;
        }

        public void ReleaseChildren(UnityTransform root)
        {
            for (var index = root.childCount - 1; index >= 0; --index)
            {
                var child = root.GetChild(index);
                _hierarchy.Unload(child);
                _nodePool.QueueFree(child);
            }
        }
    }
}
