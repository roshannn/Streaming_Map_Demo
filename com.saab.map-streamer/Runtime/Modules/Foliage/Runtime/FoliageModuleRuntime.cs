using System;
using System.Collections.Generic;
using System.Linq;

using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.Runtime;
using Saab.Utility.GfxCaps;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageModuleRuntime :
        IMapModule,
        IMapEventHandler<TerrainAddedEvent>,
        IMapEventHandler<TerrainRemovedEvent>,
        IMapEventHandler<StreamingFrameCompletedEvent>
    {
        private readonly FoliageModuleConfiguration _configuration;
        private readonly CameraControl _cameraControl;
        private readonly IMapCoordinates _mapCoordinates;
        private readonly Dictionary<SettingsFeatureType, SettingsFeature>
            _settings =
                new Dictionary<SettingsFeatureType, SettingsFeature>();
        private readonly List<FoliageFeatureRuntime> _features =
            new List<FoliageFeatureRuntime>();
        private FoliageTerrainRegistry _terrain;
        private FoliageTerrainProcessor _processor;
        private FoliageFrameCoordinator _frames;
        private FoliageResourcePool _pool;
        private bool _initialized;

        public FoliageModuleRuntime(
            FoliageModuleConfiguration configuration,
            CameraControl cameraControl,
            IMapCoordinates mapCoordinates)
        {
            _configuration = configuration;
            _cameraControl = cameraControl;
            _mapCoordinates = mapCoordinates;
        }

        public void Initialize()
        {
            if (_initialized)
                return;
            _configuration.Validate();
            try
            {
                var assets = new FoliageAssetLibrary();
                var mapping = TerrainMapping.MapFeatureData();
                foreach (var configuration in _configuration.Features)
                {
                    var feature = FoliageFeatureRuntime.Create(
                        configuration,
                        GetSettings(configuration.SettingsType),
                        _configuration,
                        assets,
                        mapping,
                        _mapCoordinates);
                    _features.Add(feature);
                }
                _terrain = new FoliageTerrainRegistry();
                _pool = new FoliageResourcePool(
                    _configuration.ResourcePoolBytes);
                _processor = new FoliageTerrainProcessor(
                    _configuration.ComputeShader,
                    _mapCoordinates,
                    _pool);
                _frames = new FoliageFrameCoordinator(
                    _configuration,
                    _cameraControl);
                _initialized = true;
            }
            catch
            {
                DisposeOwnedResources();
                throw;
            }
        }

        public void Handle(in TerrainAddedEvent mapEvent)
        {
            if (!_initialized ||
                _configuration.Disabled ||
                _terrain.Contains(mapEvent.Terrain.Identity))
                return;
            var terrain = mapEvent.Terrain;
            using (var build = _processor.Build(
                       in terrain,
                       _features,
                       GetSettings))
            {
                if (build == null)
                    return;
                var state = build.Commit();
                try
                {
                    _terrain.Add(state);
                }
                catch
                {
                    state.Dispose();
                    throw;
                }
            }
        }

        public void Handle(in TerrainRemovedEvent mapEvent)
        {
            if (_initialized)
                _terrain.Remove(mapEvent.Terrain.Identity);
        }

        public void Handle(in StreamingFrameCompletedEvent mapEvent)
        {
            if (_initialized)
                _frames.Render(_features);
        }

        public void Shutdown()
        {
            if (!_initialized &&
                _features.Count == 0 &&
                _terrain == null &&
                _frames == null &&
                _pool == null)
                return;
            _initialized = false;
            DisposeOwnedResources();
        }

        private SettingsFeature GetSettings(SettingsFeatureType type)
        {
            if (!_settings.TryGetValue(type, out var settings))
            {
                settings = GfxCaps.GetFoliageSettings(type);
                _settings.Add(type, settings);
            }
            return settings;
        }

        private void DisposeOwnedResources()
        {
            _terrain?.Dispose();
            _terrain = null;
            for (var index = _features.Count - 1; index >= 0; --index)
                _features[index].Dispose();
            _features.Clear();
            _frames?.Dispose();
            _frames = null;
            _processor = null;
            _pool?.Dispose();
            _pool = null;
            _settings.Clear();
        }
    }
}
