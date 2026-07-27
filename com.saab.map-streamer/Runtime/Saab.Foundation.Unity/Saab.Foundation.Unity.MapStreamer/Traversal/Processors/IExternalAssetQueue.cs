// Copyright 2021 saab AB

using System.Collections;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal interface IExternalAssetQueue
    {
        void Enqueue(GameObject parent, string resourceUrl, string objectId);
        IEnumerator Process();
        void Clear();
    }
}
