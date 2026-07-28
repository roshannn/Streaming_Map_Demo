using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Networking;

namespace Saab.Foundation.Unity.MapStreamer.ExternalAssets
{
    internal sealed class ExternalAssetLoader :
        IExternalAssetQueue,
        IExternalAssetProcessor,
        IExternalAssetResetter
    {
        private readonly Stack<ExternalAssetRequest> _pending =
            new Stack<ExternalAssetRequest>(100);
        private readonly Dictionary<string, AssetBundle> _bundles =
            new Dictionary<string, AssetBundle>();

        private CancellationTokenSource _processingCancellation;
        private Task _processingTask;
        private UnityWebRequest _activeRequest;

        public void Enqueue(GameObject parent, string resourceUrl, string objectId)
        {
            _pending.Push(new ExternalAssetRequest(parent, resourceUrl, objectId));
        }

        public void StartProcessing()
        {
            if (_processingTask != null)
                return;

            _processingCancellation = new CancellationTokenSource();
            _processingTask = ProcessAsync(_processingCancellation.Token);
        }

        public void StopProcessing()
        {
            var cancellation = _processingCancellation;
            var processingTask = _processingTask;

            _processingCancellation = null;
            _processingTask = null;

            cancellation?.Cancel();
            _activeRequest?.Abort();

            if (processingTask != null)
            {
                processingTask.ContinueWith(
                    task =>
                    {
                        if (task.IsFaulted)
                        {
                            var ignored = task.Exception;
                        }

                        cancellation?.Dispose();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        public void Clear()
        {
            _pending.Clear();
        }

        private async Task ProcessAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_pending.Count > 0)
                        await LoadAsync(_pending.Pop(), cancellationToken);

                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task LoadAsync(
            ExternalAssetRequest requestInfo,
            CancellationToken cancellationToken)
        {
            if (!_bundles.TryGetValue(requestInfo.ResourceUrl, out var assetBundle))
            {
                using (var request = UnityWebRequestAssetBundle.GetAssetBundle(
                           requestInfo.ResourceUrl,
                           0))
                {
                    _activeRequest = request;
                    try
                    {
                        var operation = request.SendWebRequest();
                        while (!operation.isDone)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await Task.Yield();
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        assetBundle =
                            DownloadHandlerAssetBundle.GetContent(request);
                        if (assetBundle)
                        {
                            _bundles.Add(
                                requestInfo.ResourceUrl,
                                assetBundle);
                        }
                    }
                    finally
                    {
                        if (ReferenceEquals(_activeRequest, request))
                            _activeRequest = null;
                    }
                }
            }

            if (assetBundle == null)
                return;

            var prefab = assetBundle.LoadAsset<GameObject>(requestInfo.ObjectId);
            if (prefab == null)
                return;

            var instance = UnityEngine.Object.Instantiate(prefab);
            if (instance == null)
                return;

            instance.name = requestInfo.ObjectId;
            instance.transform.SetParent(requestInfo.Parent.transform, false);
        }
    }
}
