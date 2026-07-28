using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.Building.Coordination;
using Saab.Foundation.Unity.MapStreamer.ExternalAssets;
using Saab.Foundation.Unity.MapStreamer.Maps;
using Saab.Foundation.Unity.MapStreamer.Sdk;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Streaming.Native;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Runtime
{
    internal sealed class StreamingRuntimeController : IStreamingRuntimeState
    {
        private readonly BuilderLifecycleController _builders;
        private readonly RuntimeMapStreamerSettings _settings;
        private readonly DynamicNodeLoadCoordinator _dynamicNodeLoads;
        private readonly GizmoDynamicLoaderController _dynamicLoaders;
        private readonly SceneTraverser _sceneTraverser;
        private readonly NativeSceneResources _nativeScene;
        private readonly IExternalAssetProcessor _externalAssets;
        private readonly MapLifecycleController _mapLifecycle;

        private bool _dynamicLoadsSubscribed;
        private bool _nativeSceneInitialized;
        private bool _externalAssetsProcessing;

        public StreamingRuntimeController(
            BuilderLifecycleController builders,
            RuntimeMapStreamerSettings settings,
            DynamicNodeLoadCoordinator dynamicNodeLoads,
            GizmoDynamicLoaderController dynamicLoaders,
            SceneTraverser sceneTraverser,
            NativeSceneResources nativeScene,
            IExternalAssetProcessor externalAssets,
            MapLifecycleController mapLifecycle)
        {
            _builders = builders;
            _settings = settings;
            _dynamicNodeLoads = dynamicNodeLoads;
            _dynamicLoaders = dynamicLoaders;
            _sceneTraverser = sceneTraverser;
            _nativeScene = nativeScene;
            _externalAssets = externalAssets;
            _mapLifecycle = mapLifecycle;
        }

        public bool IsInitialized { get; private set; }
        public CullTraverseAction TraverseAction { get; private set; }
        public GizmoSDK.Gizmo3D.Camera NativeCamera => _nativeScene.Camera;
        public Context NativeContext => _nativeScene.Context;

        public bool Initialize()
        {
            if (IsInitialized)
                return true;
            if (!GizmoSdkRuntime.Initialize())
                return false;

            try
            {
                _builders.Initialize();
                if (!_builders.SupportsInstancing)
                {
                    _settings.Options |=
                        MapStreamerOptions.DisableInstancing;
                }

                _dynamicNodeLoads.Subscribe();
                _dynamicLoadsSubscribed = true;
                _sceneTraverser.SetActionReceiver(
                    _dynamicNodeLoads.ActionReceiver);

                NodeLock.WaitLockEdit();
                try
                {
                    _nativeScene.Initialize();
                    _nativeSceneInitialized = true;
                    TraverseAction = new CullTraverseAction();
                }
                finally
                {
                    NodeLock.UnLock();
                }

                _dynamicLoaders.Start(_settings.DynamicLoaders);
                _externalAssets.StartProcessing();
                _externalAssetsProcessing = true;
                IsInitialized = true;
                return true;
            }
            catch
            {
                try
                {
                    ShutdownResources();
                }
                finally
                {
                    GizmoSdkRuntime.Shutdown();
                }

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
                GizmoSdkRuntime.Shutdown();
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
                    _dynamicLoaders.Stop();
                    if (_nativeSceneInitialized)
                        _mapLifecycle.Reset();
                }
                finally
                {
                    try
                    {
                        if (_nativeSceneInitialized)
                        {
                            NodeLock.WaitLockEdit();
                            try
                            {
                                _nativeScene.Dispose();
                                _nativeSceneInitialized = false;
                                TraverseAction = null;
                            }
                            finally
                            {
                                NodeLock.UnLock();
                            }
                        }
                    }
                    finally
                    {
                        if (_dynamicLoadsSubscribed)
                        {
                            _dynamicNodeLoads.Unsubscribe();
                            _sceneTraverser.SetActionReceiver(null);
                            _dynamicLoadsSubscribed = false;
                        }
                    }

                }
            }
        }
    }
}
