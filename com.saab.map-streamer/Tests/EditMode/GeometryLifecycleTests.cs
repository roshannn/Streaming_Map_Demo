using System;
using System.Collections.Generic;

using NUnit.Framework;

using Saab.Foundation.Unity.MapStreamer.Streaming;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public sealed class GeometryLifecycleTests
    {
        private sealed class EventSource : IDynamicLoadEventSource
        {
            public event Action<DynamicLoadEvent> LoadChanged;
            public event Action<NodeActivationEvent> ActivationChanged;

            public void Subscribe() { }
            public void Unsubscribe() { }

            public void Raise(DynamicLoadEvent change) =>
                LoadChanged?.Invoke(change);

            public void Raise(NodeActivationEvent change) =>
                ActivationChanged?.Invoke(change);
        }

        private sealed class NodeHandle : INativeNodeHandle
        {
            private readonly IReadOnlyList<GameObject> _objects;

            public NodeHandle(
                long identity,
                params GameObject[] gameObjects)
            {
                Identity = identity;
                _objects = gameObjects;
            }

            public long Identity { get; }
            public int DisposeCount { get; private set; }

            public GameObject Traverse() => null;

            public bool TryFindGameObjects(
                out IReadOnlyList<GameObject> gameObjects)
            {
                gameObjects = _objects;
                return true;
            }

            public void Dispose() => ++DisposeCount;
        }

        private sealed class LoaderHandle : INativeLoaderHandle
        {
            private readonly IReadOnlyList<GameObject> _objects;

            public LoaderHandle(
                long identity,
                params GameObject[] gameObjects)
            {
                Identity = identity;
                _objects = gameObjects;
            }

            public long Identity { get; }
            public int DisposeCount { get; private set; }

            public Transform FindAnchor() => null;

            public bool TryFindGameObjects(
                out IReadOnlyList<GameObject> gameObjects)
            {
                gameObjects = _objects;
                return true;
            }

            public void Dispose() => ++DisposeCount;
        }

        private sealed class HierarchyRelease : IStreamedHierarchyRelease
        {
            public readonly List<Transform> Released =
                new List<Transform>();

            public void ReleaseChildren(Transform root) =>
                Released.Add(root);
        }

        [Test]
        public void NonTraversableActivationDeactivatesEveryMappedObject()
        {
            var first = new GameObject("Building A");
            var second = new GameObject("Building B");
            try
            {
                var source = new EventSource();
                var release = new HierarchyRelease();
                var coordinator = new DynamicLoadCoordinator(source, release);
                var node = new NodeHandle(17, first, second);

                coordinator.Subscribe();
                source.Raise(
                    new NodeActivationEvent(
                        NodeActivationState.NotTraversable,
                        node));
                coordinator.ProcessActivations();

                Assert.That(first.activeSelf, Is.False);
                Assert.That(second.activeSelf, Is.False);
                Assert.That(node.DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void LatestActivationForIdentityWinsWithinFrame()
        {
            var building = new GameObject("Building");
            try
            {
                building.SetActive(false);
                var source = new EventSource();
                var coordinator =
                    new DynamicLoadCoordinator(source, new HierarchyRelease());
                var hidden = new NodeHandle(23, building);
                var visible = new NodeHandle(23, building);

                coordinator.Subscribe();
                source.Raise(
                    new NodeActivationEvent(
                        NodeActivationState.NotTraversable,
                        hidden));
                source.Raise(
                    new NodeActivationEvent(
                        NodeActivationState.Traversable,
                        visible));
                coordinator.ProcessActivations();

                Assert.That(building.activeSelf, Is.True);
                Assert.That(hidden.DisposeCount, Is.EqualTo(1));
                Assert.That(visible.DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(building);
            }
        }

        [Test]
        public void UnloadedLoaderReleasesEveryMappedHierarchy()
        {
            var first = new GameObject("Loader A");
            var second = new GameObject("Loader B");
            try
            {
                var source = new EventSource();
                var release = new HierarchyRelease();
                var coordinator = new DynamicLoadCoordinator(source, release);
                var loader = new LoaderHandle(31, first, second);
                var node = new NodeHandle(41);

                coordinator.Subscribe();
                source.Raise(
                    new DynamicLoadEvent(
                        DynamicLoadState.Unloaded,
                        loader,
                        node));
                coordinator.ProcessLoads();

                Assert.That(
                    release.Released,
                    Is.EquivalentTo(
                        new[] { first.transform, second.transform }));
                Assert.That(loader.DisposeCount, Is.EqualTo(1));
                Assert.That(node.DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }
    }
}
