using System;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    public sealed class MapLifecycleController : IMapRuntime
    {
        private readonly IStreamingLock _streamingLock;
        private readonly IMapConfiguration _configuration;
        private readonly IMapDataSource _maps;
        private readonly IMapInstaller _installer;
        private readonly IStreamingContentResetter _contentResetter;
        private GameObject _root;

        public MapLifecycleController(
            IStreamingLock streamingLock,
            IMapConfiguration configuration,
            IMapDataSource maps,
            IMapInstaller installer,
            IStreamingContentResetter contentResetter)
        {
            _streamingLock = streamingLock;
            _configuration = configuration;
            _maps = maps;
            _installer = installer;
            _contentResetter = contentResetter;
        }

        public event Action OnMapChanged;
        public event MapLoadErrorHandler OnMapLoadError;

        public bool LoadMap()
        {
            var mapUrl = _configuration.MapUrl;
            while (true)
            {
                if (TryLoadAndInstall(mapUrl, out var failure))
                {
                    OnMapChanged?.Invoke();
                    return true;
                }

                var retry = false;
                OnMapLoadError?.Invoke(ref mapUrl, failure, ref retry);
                if (!retry)
                    return false;
            }
        }

        public void Reset()
        {
            _streamingLock.AcquireEdit();
            try
            {
                ResetLocked();
            }
            finally
            {
                _streamingLock.Release();
            }
        }

        private void ResetLocked()
        {
            _contentResetter.Reset(_root);
            _root = null;
        }

        private bool TryLoadAndInstall(
            string mapUrl,
            out MapLoadFailure failure)
        {
            failure = default;
            _streamingLock.AcquireEdit();
            INativeMapHandle loadedMap = null;
            try
            {
                if (!string.IsNullOrEmpty(mapUrl) &&
                    !_maps.TryLoad(mapUrl, out loadedMap, out failure))
                    return false;

                ResetLocked();
                _root = _installer.Install(mapUrl, loadedMap);
                loadedMap = null;
                return true;
            }
            finally
            {
                loadedMap?.Dispose();
                _streamingLock.Release();
            }
        }
    }
}
