using System;
using System.Collections.Generic;

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Utility.Unity.NodeUtils;

using UnityEngine;
using unTransform = UnityEngine.Transform;

namespace Saab.Foundation.Unity.MapStreamer.DynamicLoading
{
    internal sealed class DynamicNodeLoadCoordinator : IDisposable
    {
        private const string MessageSource =
            "Saab.Foundation.Unity.MapStreamer.DynamicNodeLoadCoordinator";

        private readonly Func<Node, GameObject> _traverse;
        private readonly Action<unTransform> _unloadHierarchy;
        private readonly Action<unTransform> _free;
        private readonly List<PendingDynamicLoad> _pendingLoads =
            new List<PendingDynamicLoad>(100);
        private readonly Dictionary<IntPtr, PendingDynamicLoad> _activeLoads =
            new Dictionary<IntPtr, PendingDynamicLoad>();
        private readonly List<PendingNodeActivation> _pendingActivations =
            new List<PendingNodeActivation>(100);

        private bool _subscribed;

        public DynamicNodeLoadCoordinator(
            SceneTraverser traverser,
            NodeHierarchyUnloader hierarchyUnloader,
            NodeHandlePool nodeHandlePool)
        {
            _traverse = traverser.Begin;
            _unloadHierarchy = hierarchyUnloader.Unload;
            _free = nodeHandlePool.QueueFree;

            ActionReceiver = new NodeAction("DynamicLoadManager");
        }

        public NodeAction ActionReceiver { get; }
        public bool HasPendingLoads => _activeLoads.Count > 0;

        public void Subscribe()
        {
            if (_subscribed)
                return;

            ActionReceiver.OnAction += OnAction;
            DynamicLoader.OnDynamicLoad += OnDynamicLoad;
            _subscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_subscribed)
                return;

            DynamicLoader.OnDynamicLoad -= OnDynamicLoad;
            ActionReceiver.OnAction -= OnAction;
            _subscribed = false;
        }

        public void ProcessLoads()
        {
            foreach (var pendingLoad in _pendingLoads)
            {
                if (pendingLoad.State == DynamicLoadingState.LOADED)
                {
                    var transform =
                        NodeUtils.FindFirstGameObjectTransformUnsafe(
                            pendingLoad.Loader.GetNativeReference());

                    if (transform == null || transform.childCount != 0)
                        continue;

                    var gameObject = _traverse(pendingLoad.Node);
                    if (gameObject != null)
                        gameObject.transform.SetParent(transform, false);
                }
                else if (pendingLoad.State == DynamicLoadingState.UNLOADED &&
                         NodeUtils.FindGameObjectsUnsafe(
                             pendingLoad.Loader.GetNativeReference(),
                             out List<GameObject> gameObjects))
                {
                    foreach (var gameObject in gameObjects)
                    {
                        var transform = gameObject.transform;
                        for (var i = transform.childCount - 1; i >= 0; --i)
                        {
                            var child = transform.GetChild(i);
                            _unloadHierarchy(child);
                            _free(child);
                        }
                    }
                }
            }

            _pendingLoads.Clear();
            _activeLoads.Clear();
        }

        public void ProcessActivations()
        {
            foreach (var activation in _pendingActivations)
            {
                if (NodeUtils.FindGameObjectsUnsafe(
                    activation.Node.GetNativeReference(),
                    out List<GameObject> gameObjects))
                {
                    foreach (var gameObject in gameObjects)
                    {
                        if (activation.State == NodeActionEvent.IS_TRAVERSABLE)
                            gameObject.SetActive(true);
                        else if (activation.State == NodeActionEvent.IS_NOT_TRAVERSABLE)
                            gameObject.SetActive(false);
                    }
                }
                else
                {
                    Message.Send(
                        MessageSource,
                        MessageLevel.DEBUG,
                        $"Got Activation {activation.State} for missing node");
                }
            }

            _pendingActivations.Clear();
        }

        public void Reset()
        {
            foreach (var pendingLoad in _pendingLoads)
            {
                pendingLoad.Loader?.Dispose();
                pendingLoad.Node?.Dispose();
            }

            _pendingLoads.Clear();
            _activeLoads.Clear();

            foreach (var activation in _pendingActivations)
                activation.Node?.Dispose();

            _pendingActivations.Clear();
        }

        public void Dispose()
        {
            Unsubscribe();
            Reset();
            ActionReceiver.Dispose();
        }

        private void OnAction(
            NodeAction sender,
            NodeActionEvent action,
            Context context,
            NodeActionProvider trigger,
            TraverseAction traverser,
            IntPtr userdata)
        {
            if (action == NodeActionEvent.IS_TRAVERSABLE ||
                action == NodeActionEvent.IS_NOT_TRAVERSABLE)
            {
                _pendingActivations.Add(
                    new PendingNodeActivation(action, trigger as Node));
            }
            else
            {
                trigger?.ReleaseNoDelete();
            }

            traverser?.ReleaseNoDelete();
            context?.ReleaseNoDelete();
        }

        private void OnDynamicLoad(
            DynamicLoadingState state,
            DynamicLoader loader,
            Node node)
        {
            if (state != DynamicLoadingState.LOADED &&
                state != DynamicLoadingState.UNLOADED)
            {
                loader?.ReleaseNoDelete();
                node?.ReleaseNoDelete();
                return;
            }

            var nativeReference = loader.GetNativeReference();
            if (!_activeLoads.TryGetValue(nativeReference, out var activeLoad))
            {
                var pendingLoad = new PendingDynamicLoad(state, loader, node);
                _pendingLoads.Add(pendingLoad);
                _activeLoads.Add(nativeReference, pendingLoad);
                return;
            }

            if (activeLoad.State == state)
            {
                // Cached LOD data may report the same transition more than once.
                // It is still one pending operation, not a balanced cancellation.
                loader?.ReleaseNoDelete();
                node?.ReleaseNoDelete();
                return;
            }

            for (var i = _pendingLoads.Count - 1; i >= 0; --i)
            {
                if (_pendingLoads[i].Loader.GetNativeReference() == nativeReference)
                {
                    _pendingLoads.RemoveAt(i);
                    break;
                }
            }

            _activeLoads.Remove(nativeReference);
            activeLoad.Loader?.ReleaseNoDelete();
            activeLoad.Node?.ReleaseNoDelete();
            loader?.ReleaseNoDelete();
            node?.ReleaseNoDelete();
        }
    }
}
