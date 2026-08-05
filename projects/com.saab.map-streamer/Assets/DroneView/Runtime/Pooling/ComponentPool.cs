using System;
using System.Collections.Generic;
using UnityEngine;

namespace StreamingMapDemo.Pooling
{
    /// <summary>A reusable, expanding pool for Unity component prefabs.</summary>
    public sealed class ComponentPool<T> : IDisposable where T : Component
    {
        private readonly T prefab;
        private readonly Transform parent;
        private readonly Queue<T> available = new Queue<T>();
        private readonly HashSet<T> leased = new HashSet<T>();
        private bool disposed;

        public int AvailableCount => available.Count;
        public int LeasedCount => leased.Count;
        public int Count => AvailableCount + LeasedCount;

        public ComponentPool(T prefab, int initialCapacity = 0, Transform parent = null)
        {
            this.prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            this.parent = parent;
            Prewarm(initialCapacity);
        }

        public void Prewarm(int count)
        {
            ThrowIfDisposed();
            for (int i = Count; i < Mathf.Max(0, count); i++)
            {
                available.Enqueue(CreateInstance());
            }
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            ThrowIfDisposed();
            T instance = available.Count > 0 ? available.Dequeue() : CreateInstance();
            leased.Add(instance);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            return instance;
        }

        public bool Release(T instance)
        {
            if (disposed || instance == null || !leased.Remove(instance))
            {
                return false;
            }

            instance.gameObject.SetActive(false);
            instance.transform.SetParent(parent, false);
            available.Enqueue(instance);
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (T instance in available) DestroyInstance(instance);
            foreach (T instance in leased) DestroyInstance(instance);
            available.Clear();
            leased.Clear();
        }

        private T CreateInstance()
        {
            T instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private static void DestroyInstance(T instance)
        {
            if (instance == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance.gameObject);
            else UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(GetType().Name);
        }
    }
}
