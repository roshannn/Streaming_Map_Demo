// Copyright 2021 saab AB

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class ExternalAssetLoader : IExternalAssetQueue
    {
        private readonly Stack<ExternalAssetRequest> _pending =
            new Stack<ExternalAssetRequest>(100);
        private readonly Dictionary<string, AssetBundle> _bundles =
            new Dictionary<string, AssetBundle>();

        public void Enqueue(GameObject parent, string resourceUrl, string objectId)
        {
            _pending.Push(new ExternalAssetRequest(parent, resourceUrl, objectId));
        }

        public IEnumerator Process()
        {
            while (true)
            {
                if (_pending.Count > 0)
                    yield return Load(_pending.Pop());

                yield return null;
            }
        }

        public void Clear()
        {
            _pending.Clear();
        }

        private IEnumerator Load(ExternalAssetRequest requestInfo)
        {
            if (!_bundles.TryGetValue(requestInfo.ResourceUrl, out var assetBundle))
            {
                var request =
                    UnityWebRequestAssetBundle.GetAssetBundle(requestInfo.ResourceUrl, 0);
                yield return request.SendWebRequest();

                assetBundle = DownloadHandlerAssetBundle.GetContent(request);
                if (assetBundle)
                    _bundles.Add(requestInfo.ResourceUrl, assetBundle);
            }

            if (assetBundle == null)
                yield break;

            var prefab = assetBundle.LoadAsset<GameObject>(requestInfo.ObjectId);
            if (prefab == null)
                yield break;

            var instance = Object.Instantiate(prefab);
            if (instance == null)
                yield break;

            instance.name = requestInfo.ObjectId;
            instance.transform.SetParent(requestInfo.Parent.transform, false);
        }
    }
}
