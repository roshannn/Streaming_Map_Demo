// Copyright 2021 saab AB

using System;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Events
{
    public sealed class NodeEvents : MonoBehaviour
    {
        public event Action<GameObject, bool> TerrainCreated;
        public event Action<GameObject, bool> GeometryCreated;
        public event Action<GameObject, bool> CrossboardCreated;
        public event Action<GameObject, bool> LodCreated;
        public event Action<GameObject, bool> LoaderCreated;
        public event Action<GameObject> EnteredPool;
        public event Action<GameObject> GeometryRemoved;
        public event Action<GameObject, byte> TerrainRemoved;

        public void NotifyTerrainCreated(GameObject gameObject, bool isAsset) =>
            TerrainCreated?.Invoke(gameObject, isAsset);
        public void NotifyGeometryCreated(GameObject gameObject, bool isAsset) =>
            GeometryCreated?.Invoke(gameObject, isAsset);
        public void NotifyCrossboardCreated(GameObject gameObject, bool isAsset) =>
            CrossboardCreated?.Invoke(gameObject, isAsset);
        public void NotifyLodCreated(GameObject gameObject, bool isAsset) =>
            LodCreated?.Invoke(gameObject, isAsset);
        public void NotifyLoaderCreated(GameObject gameObject, bool isAsset) =>
            LoaderCreated?.Invoke(gameObject, isAsset);
        public void NotifyEnteredPool(GameObject gameObject) =>
            EnteredPool?.Invoke(gameObject);
        public void NotifyGeometryRemoved(GameObject gameObject) =>
            GeometryRemoved?.Invoke(gameObject);
        public void NotifyTerrainRemoved(
            GameObject gameObject,
            byte nodeVersion) =>
            TerrainRemoved?.Invoke(gameObject, nodeVersion);
    }
}
