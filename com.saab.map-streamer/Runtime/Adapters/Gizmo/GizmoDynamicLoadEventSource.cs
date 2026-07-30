using System;
using System.Collections.Generic;

using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.Streaming;

using UnityEngine;
using UnityTransform = UnityEngine.Transform;

namespace Saab.Foundation.Unity.MapStreamer.GizmoAdapter
{
    public interface IGizmoDynamicLoadCallbacks
    {
        void SetActionReceiver(NodeAction receiver);
        GameObject Traverse(Node node);
        UnityTransform FindLoaderAnchor(IntPtr nativeReference);
        bool TryFindGameObjects(
            IntPtr nativeReference,
            out IReadOnlyList<GameObject> gameObjects);
    }

    public sealed class GizmoDynamicLoadEventSource :
        IDynamicLoadEventSource,
        IDisposable
    {
        private readonly IGizmoDynamicLoadCallbacks _callbacks;
        private readonly NodeAction _actionReceiver =
            new NodeAction("DynamicLoadManager");
        private bool _subscribed;

        public GizmoDynamicLoadEventSource(IGizmoDynamicLoadCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        public event Action<DynamicLoadEvent> LoadChanged;
        public event Action<NodeActivationEvent> ActivationChanged;

        public void Subscribe()
        {
            if (_subscribed)
                return;

            _actionReceiver.OnAction += OnAction;
            DynamicLoader.OnDynamicLoad += OnDynamicLoad;
            _callbacks.SetActionReceiver(_actionReceiver);
            _subscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_subscribed)
                return;

            _callbacks.SetActionReceiver(null);
            DynamicLoader.OnDynamicLoad -= OnDynamicLoad;
            _actionReceiver.OnAction -= OnAction;
            _subscribed = false;
        }

        public void Dispose()
        {
            Unsubscribe();
            _actionReceiver.Dispose();
        }

        private void OnAction(
            NodeAction sender,
            NodeActionEvent action,
            Context context,
            NodeActionProvider trigger,
            TraverseAction traverser,
            IntPtr userdata)
        {
            try
            {
                if (trigger is Node node &&
                    (action == NodeActionEvent.IS_TRAVERSABLE ||
                     action == NodeActionEvent.IS_NOT_TRAVERSABLE))
                {
                    var state =
                        action == NodeActionEvent.IS_TRAVERSABLE
                            ? NodeActivationState.Traversable
                            : NodeActivationState.NotTraversable;
                    ActivationChanged?.Invoke(
                        new NodeActivationEvent(
                            state,
                            new GizmoNodeHandle(node, _callbacks)));
                }
                else
                {
                    trigger?.ReleaseNoDelete();
                }
            }
            finally
            {
                traverser?.ReleaseNoDelete();
                context?.ReleaseNoDelete();
            }
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

            var neutralState =
                state == DynamicLoadingState.LOADED
                    ? DynamicLoadState.Loaded
                    : DynamicLoadState.Unloaded;
            LoadChanged?.Invoke(
                new DynamicLoadEvent(
                    neutralState,
                    new GizmoLoaderHandle(loader, _callbacks),
                    new GizmoNodeHandle(node, _callbacks)));
        }
    }

    internal sealed class GizmoLoaderHandle : INativeLoaderHandle
    {
        private DynamicLoader _loader;
        private readonly IGizmoDynamicLoadCallbacks _callbacks;

        public GizmoLoaderHandle(
            DynamicLoader loader,
            IGizmoDynamicLoadCallbacks callbacks)
        {
            _loader = loader;
            _callbacks = callbacks;
            Identity = loader?.GetNativeReference().ToInt64() ?? 0;
        }

        public long Identity { get; }

        public UnityTransform FindAnchor() =>
            _loader == null
                ? null
                : _callbacks.FindLoaderAnchor(_loader.GetNativeReference());

        public bool TryFindGameObjects(
            out IReadOnlyList<GameObject> gameObjects)
        {
            if (_loader == null)
            {
                gameObjects = Array.Empty<GameObject>();
                return false;
            }

            return _callbacks.TryFindGameObjects(
                _loader.GetNativeReference(),
                out gameObjects);
        }

        public void Dispose()
        {
            _loader?.ReleaseNoDelete();
            _loader = null;
        }
    }

    internal sealed class GizmoNodeHandle : INativeNodeHandle
    {
        private Node _node;
        private readonly IGizmoDynamicLoadCallbacks _callbacks;
        private bool _ownershipTransferred;

        public GizmoNodeHandle(
            Node node,
            IGizmoDynamicLoadCallbacks callbacks)
        {
            _node = node;
            _callbacks = callbacks;
            Identity = node?.GetNativeReference().ToInt64() ?? 0;
        }

        public long Identity { get; }

        public GameObject Traverse()
        {
            if (_node == null)
                return null;

            var gameObject = _callbacks.Traverse(_node);
            _ownershipTransferred = gameObject != null;
            return gameObject;
        }

        public bool TryFindGameObjects(
            out IReadOnlyList<GameObject> gameObjects)
        {
            if (_node == null)
            {
                gameObjects = Array.Empty<GameObject>();
                return false;
            }

            return _callbacks.TryFindGameObjects(
                _node.GetNativeReference(),
                out gameObjects);
        }

        public void Dispose()
        {
            if (!_ownershipTransferred)
                _node?.ReleaseNoDelete();
            _node = null;
        }
    }
}
