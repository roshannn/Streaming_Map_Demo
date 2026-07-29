namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    public sealed class StreamingRuntimeController : IStreamingRuntimeState
    {
        private readonly IBuilderRuntime _builders;
        private readonly IStreamingRuntimeOptions _options;
        private readonly IDynamicLoadPump _dynamicLoads;
        private readonly IDynamicLoaderRuntime _dynamicLoaderRuntime;
        private readonly IStreamingBackend _backend;
        private readonly IExternalAssetRuntime _externalAssets;
        private readonly IMapRuntime _map;
        private readonly IMapModuleRuntime _modules;

        private bool _backendInitialized;
        private bool _dynamicLoadsSubscribed;
        private bool _dynamicLoaderStarted;
        private bool _externalAssetsProcessing;
        private bool _modulesInitialized;
        private bool _runtimeStarted;

        public StreamingRuntimeController(
            IBuilderRuntime builders,
            IStreamingRuntimeOptions options,
            IDynamicLoadPump dynamicLoads,
            IDynamicLoaderRuntime dynamicLoaderRuntime,
            IStreamingBackend backend,
            IExternalAssetRuntime externalAssets,
            IMapRuntime map,
            IMapModuleRuntime modules)
        {
            _builders = builders;
            _options = options;
            _dynamicLoads = dynamicLoads;
            _dynamicLoaderRuntime = dynamicLoaderRuntime;
            _backend = backend;
            _externalAssets = externalAssets;
            _map = map;
            _modules = modules;
        }

        public bool IsInitialized { get; private set; }

        public bool Initialize()
        {
            if (IsInitialized)
                return true;
            if (!_backend.Initialize())
                return false;

            _backendInitialized = true;
            try
            {
                _builders.Initialize();
                if (!_builders.SupportsInstancing)
                    _options.DisableInstancing();

                _modules.Initialize();
                _modulesInitialized = true;

                _dynamicLoads.Subscribe();
                _dynamicLoadsSubscribed = true;

                _dynamicLoaderRuntime.Start(_options.DynamicLoaderCount);
                _dynamicLoaderStarted = true;

                _externalAssets.StartProcessing();
                _externalAssetsProcessing = true;
                _runtimeStarted = true;
                IsInitialized = true;
                return true;
            }
            catch
            {
                ShutdownResources();
                throw;
            }
        }

        public bool Shutdown()
        {
            if (!IsInitialized)
                return false;

            try
            {
                ShutdownResources();
                return true;
            }
            finally
            {
                IsInitialized = false;
            }
        }

        private void ShutdownResources()
        {
            try
            {
                if (_externalAssetsProcessing)
                {
                    _externalAssets.StopProcessing();
                    _externalAssetsProcessing = false;
                }
            }
            finally
            {
                try
                {
                    if (_dynamicLoaderStarted)
                    {
                        _dynamicLoaderRuntime.Stop();
                        _dynamicLoaderStarted = false;
                    }

                    try
                    {
                        if (_runtimeStarted)
                        {
                            _runtimeStarted = false;
                            _map.Reset();
                        }
                    }
                    finally
                    {
                        if (_modulesInitialized)
                        {
                            _modulesInitialized = false;
                            _modules.Shutdown();
                        }
                    }
                }
                finally
                {
                    try
                    {
                        if (_backendInitialized)
                        {
                            _backend.Shutdown();
                            _backendInitialized = false;
                        }
                    }
                    finally
                    {
                        if (_dynamicLoadsSubscribed)
                        {
                            _dynamicLoads.Unsubscribe();
                            _dynamicLoadsSubscribed = false;
                        }
                    }
                }
            }
        }
    }
}
