// Copyright 2021 saab AB

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Nodes.Pooling
{
    /// <summary>
    /// Defines feature-specific initialization and cleanup for pooled objects.
    /// </summary>
    public interface IPooledNodeObjectPolicy
    {
        PoolObjectFeature Feature { get; }
        void Initialize(GameObject gameObject);
        void Reset(GameObject gameObject, bool sharedAsset);
    }
}
