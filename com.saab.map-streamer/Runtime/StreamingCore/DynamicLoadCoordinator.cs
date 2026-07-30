using System;
using System.Collections.Generic;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    public sealed class DynamicLoadCoordinator : IDynamicLoadPump, IDisposable
    {
        private readonly IDynamicLoadEventSource _events;
        private readonly IStreamedHierarchyRelease _hierarchy;
        private readonly List<DynamicLoadEvent> _pendingLoads =
            new List<DynamicLoadEvent>(100);
        private readonly Dictionary<long, DynamicLoadEvent> _activeLoads =
            new Dictionary<long, DynamicLoadEvent>();
        private readonly Dictionary<long, NodeActivationEvent>
            _pendingActivations =
                new Dictionary<long, NodeActivationEvent>(100);
        private bool _subscribed;

        public DynamicLoadCoordinator(
            IDynamicLoadEventSource events,
            IStreamedHierarchyRelease hierarchy)
        {
            _events = events;
            _hierarchy = hierarchy;
        }

        public bool HasPendingLoads => _activeLoads.Count > 0;

        public void Subscribe()
        {
            if (_subscribed)
                return;

            _events.LoadChanged += OnLoadChanged;
            _events.ActivationChanged += OnActivationChanged;
            _events.Subscribe();
            _subscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_subscribed)
                return;

            _events.Unsubscribe();
            _events.ActivationChanged -= OnActivationChanged;
            _events.LoadChanged -= OnLoadChanged;
            _subscribed = false;
        }

        public void ProcessLoads()
        {
            foreach (var pending in _pendingLoads)
            {
                try
                {
                    if (pending.State == DynamicLoadState.Loaded)
                    {
                        var anchor = pending.Loader.FindAnchor();
                        if (anchor == null || anchor.childCount != 0)
                            continue;

                        var gameObject = pending.Node.Traverse();
                        if (gameObject != null)
                        {
                            gameObject.transform.SetParent(anchor, false);
                        }
                    }
                    else if (pending.Loader.TryFindGameObjects(
                                 out var gameObjects))
                    {
                        foreach (var gameObject in gameObjects)
                        {
                            _hierarchy.ReleaseChildren(gameObject.transform);
                        }
                    }
                }
                finally
                {
                    pending.Loader.Dispose();
                    pending.Node.Dispose();
                }
            }

            _pendingLoads.Clear();
            _activeLoads.Clear();
        }

        public void ProcessActivations()
        {
            foreach (var activation in _pendingActivations.Values)
            {
                try
                {
                    if (!activation.Node.TryFindGameObjects(out var gameObjects))
                        continue;

                    var active =
                        activation.State == NodeActivationState.Traversable;
                    foreach (var gameObject in gameObjects)
                        gameObject.SetActive(active);
                }
                finally
                {
                    activation.Node.Dispose();
                }
            }

            _pendingActivations.Clear();
        }

        public void Reset()
        {
            foreach (var pending in _pendingLoads)
            {
                pending.Loader.Dispose();
                pending.Node.Dispose();
            }

            _pendingLoads.Clear();
            _activeLoads.Clear();

            foreach (var activation in _pendingActivations.Values)
                activation.Node.Dispose();

            _pendingActivations.Clear();
        }

        public void Dispose()
        {
            Unsubscribe();
            Reset();
            if (_events is IDisposable disposableEvents)
                disposableEvents.Dispose();
        }

        private void OnActivationChanged(NodeActivationEvent activation)
        {
            var identity = activation.Node.Identity;
            if (_pendingActivations.TryGetValue(identity, out var previous))
                previous.Node.Dispose();

            _pendingActivations[identity] = activation;
        }

        private void OnLoadChanged(DynamicLoadEvent change)
        {
            var identity = change.Loader.Identity;
            if (!_activeLoads.TryGetValue(identity, out var active))
            {
                _pendingLoads.Add(change);
                _activeLoads.Add(identity, change);
                return;
            }

            if (active.State == change.State)
            {
                change.Loader.Dispose();
                change.Node.Dispose();
                return;
            }

            for (var index = _pendingLoads.Count - 1; index >= 0; --index)
            {
                if (_pendingLoads[index].Loader.Identity == identity)
                {
                    _pendingLoads.RemoveAt(index);
                    break;
                }
            }

            _activeLoads.Remove(identity);
            active.Loader.Dispose();
            active.Node.Dispose();
            change.Loader.Dispose();
            change.Node.Dispose();
        }
    }
}
