// Copyright 2021 saab AB

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal readonly struct ExternalAssetRequest
    {
        public ExternalAssetRequest(
            GameObject parent,
            string resourceUrl,
            string objectId)
        {
            Parent = parent;
            ResourceUrl = resourceUrl;
            ObjectId = objectId;
        }

        public GameObject Parent { get; }
        public string ResourceUrl { get; }
        public string ObjectId { get; }
    }
}
