// Copyright 2021 saab AB

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.ExternalAssets
{
    internal interface IExternalAssetQueue
    {
        void Enqueue(GameObject parent, string resourceUrl, string objectId);
    }
}
