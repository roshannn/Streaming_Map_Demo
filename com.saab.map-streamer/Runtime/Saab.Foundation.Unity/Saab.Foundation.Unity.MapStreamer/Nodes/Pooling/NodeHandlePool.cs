using System;
using System.Collections.Generic;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;

using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

using UnityEngine;
using unTransform = UnityEngine.Transform;

using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;

namespace Saab.Foundation.Unity.MapStreamer.Nodes.Pooling
{
    internal sealed class NodeHandlePool
    {
        private readonly Stack<NodeHandle>[] _free =
            new Stack<NodeHandle>[byte.MaxValue];
        private readonly NodeHandle[] _prefabs =
            new NodeHandle[byte.MaxValue];
        private readonly Stack<unTransform> _pendingFrees =
            new Stack<unTransform>();
        private readonly Queue<byte> _preAllocationOrder =
            new Queue<byte>();
        private readonly TextureManager _textures;
        private readonly NodeEvents _events;
        private readonly ProfilerMarker _freeMarker =
            new ProfilerMarker(ProfilerCategory.Render, "NodePool-Free");

        public NodeHandlePool(TextureManager textures, NodeEvents events)
        {
            _textures = textures;
            _events = events;
        }

        public bool HasPool(PoolObjectFeature feature) =>
            _prefabs[(byte)feature] != null;

        public void Initialize(
            IEnumerable<IPooledNodeObjectPolicy> policies)
        {
            EnsurePool(PoolObjectFeature.None, null);

            foreach (var policy in policies)
                EnsurePool(policy.Feature, policy);

            _preAllocationOrder.Clear();
            for (var i = 0; i < _free.Length; ++i)
            {
                if (_free[i] != null)
                    _preAllocationOrder.Enqueue((byte)i);
            }
        }

        public NodeHandle Allocate(PoolObjectFeature feature, Node node)
        {
            var pool = _free[(byte)feature];
            if (pool == null)
                throw new InvalidOperationException(
                    $"No node-handle pool registered for {feature}");

            if (pool.Count == 0)
                ProcessPending(100);

            if (pool.Count > 0)
            {
                var handle = pool.Pop();
                handle.node = node;
                handle.gameObject.SetActive(true);
#if UNITY_EDITOR
                handle.gameObject.hideFlags = HideFlags.None;
#endif
                return handle;
            }

            var allocated = UnityEngine.Object.Instantiate(_prefabs[(byte)feature]);
            allocated.node = node;
            allocated.gameObject.SetActive(true);
            return allocated;
        }

        public void QueueFree(unTransform transform)
        {
            transform.parent = null;
            transform.gameObject.SetActive(false);
#if UNITY_EDITOR
            transform.hideFlags = HideFlags.HideInHierarchy;
#endif
            _pendingFrees.Push(transform);
        }

        public void ProcessPending(int count)
        {
            _freeMarker.Begin();
            while (_pendingFrees.Count > 0 && count-- > 0)
            {
                var transform = _pendingFrees.Pop();

                for (var i = transform.childCount - 1; i >= 0; --i)
                    QueueFree(transform.GetChild(i));

                Recycle(transform);
            }
            _freeMarker.End();
        }

        public void PreAllocate(int count, TimeSpan timeBudget)
        {
            if (_preAllocationOrder.Count == 0)
                return;

            var timer = System.Diagnostics.Stopwatch.StartNew();
            var fullyAllocatedPools = 0;

            while (timer.Elapsed < timeBudget &&
                   fullyAllocatedPools < _preAllocationOrder.Count)
            {
                var poolId = _preAllocationOrder.Dequeue();
                _preAllocationOrder.Enqueue(poolId);

                var pool = _free[poolId];
                var remaining = Math.Min(100, count - pool.Count);

                if (pool.Count < count)
                    AllocateForPool(poolId, remaining);
                else
                    ++fullyAllocatedPools;
            }
        }

        private void EnsurePool(
            PoolObjectFeature feature,
            IPooledNodeObjectPolicy policy)
        {
            var index = (byte)feature;
            if (_free[index] != null)
                return;

            _free[index] = new Stack<NodeHandle>(65000);
            _prefabs[index] = CreatePrefab(feature, policy);
        }

        private static NodeHandle CreatePrefab(
            PoolObjectFeature feature,
            IPooledNodeObjectPolicy policy)
        {
            var prefab = new GameObject();
            prefab.SetActive(false);
#if UNITY_EDITOR
            prefab.hideFlags = HideFlags.HideInHierarchy;
#endif
            var handle = prefab.AddComponent<NodeHandle>();
            handle.featureKey = feature;
            handle.poolPolicy = policy;
            policy?.Initialize(prefab);
            return handle;
        }

        private void AllocateForPool(byte poolId, int count)
        {
            for (var i = 0; i < count; ++i)
            {
                var handle = UnityEngine.Object.Instantiate(_prefabs[poolId]);
#if UNITY_EDITOR
                handle.gameObject.hideFlags = HideFlags.HideInHierarchy;
#endif
                _free[poolId].Push(handle);
            }
        }

        private void Recycle(unTransform transform)
        {
            if (!transform.TryGetComponent<NodeHandle>(out var handle))
            {
                UnityEngine.Object.Destroy(transform.gameObject);
                return;
            }

            _free[(byte)handle.featureKey].Push(handle);

            var gameObject = handle.gameObject;
            var objectTransform = gameObject.transform;
            objectTransform.localPosition = Vector3.zero;
            objectTransform.localRotation = Quaternion.identity;
            objectTransform.localScale = Vector3.one;

            if (handle.poolPolicy != null)
            {
                var sharedNode =
                    handle.stateFlags.HasFlag(NodeStateFlags.AssetInstance);
                handle.poolPolicy.Reset(gameObject, sharedNode);
            }

            if (handle.node is Geometry)
            {
                if (handle.featureKey == PoolObjectFeature.Terrain)
                    _events.NotifyTerrainRemoved(gameObject);
                else if (handle.featureKey == PoolObjectFeature.StaticMesh)
                    _events.NotifyGeometryRemoved(gameObject);
            }

            handle.Recycle(_textures);
            _events.NotifyEnteredPool(gameObject);
        }
    }
}
