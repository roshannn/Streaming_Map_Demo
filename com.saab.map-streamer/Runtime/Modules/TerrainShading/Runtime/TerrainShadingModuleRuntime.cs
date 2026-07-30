using System;
using System.Collections.Generic;
using System.Linq;

using Saab.Utility.GfxCaps;
using Saab.Foundation.Unity.MapStreamer.Utils;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Runtime
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Runtime;

    internal sealed class TerrainShadingModuleRuntime :
        IMapModule,
        IMapEventHandler<TerrainAddedEvent>,
        IMapEventHandler<TerrainRemovedEvent>
    {
        private readonly TerrainShadingModuleDefinition _definition;
        private readonly Dictionary<
            TerrainModuleIdentity,
            TerrainShadingState> _terrain =
                new Dictionary<TerrainModuleIdentity, TerrainShadingState>();
        private readonly TerrainMaterialBinder _binder =
            new TerrainMaterialBinder();

        private TerrainTextureLibrary _textures;
        private TerrainNormalGenerator _normalGenerator;
        private bool _initialized;

        public TerrainShadingModuleRuntime(
            TerrainShadingModuleDefinition definition)
        {
            _definition = definition;
        }

        public void Initialize()
        {
            if (_initialized)
                return;

            Shader.SetGlobalColor(
                "_TargetTerrainColor",
                _definition.TargetHue);
            Shader.SetGlobalFloat(
                "_HueShift",
                _definition.HueShiftInclusion);

            var enabled = _definition.EnableDetailedTextures &&
                GfxCaps.CurrentCaps.HasFlag(
                    Capability.UseTerrainDetailTextures);
            _textures = new TerrainTextureLibrary();
            if (enabled && !_textures.Build(_definition.DetailTextureSet))
                throw new InvalidOperationException(
                    "Terrain detail texture library could not be built.");
            _normalGenerator = new TerrainNormalGenerator(
                _definition.NormalComputeShader);
            _initialized = true;
        }

        public void Handle(in TerrainAddedEvent mapEvent)
        {
            if (!_initialized || _textures.Albedo == null)
                return;

            var terrain = mapEvent.Terrain;
            if (!terrain.FeatureTexture || !terrain.Texture)
                return;

            Remove(terrain.Identity);
            var state = _binder.Bind(
                in terrain,
                _textures,
                _normalGenerator);
            if (state != null)
                _terrain.Add(terrain.Identity, state);
        }

        public void Handle(in TerrainRemovedEvent mapEvent) =>
            Remove(mapEvent.Terrain.Identity);

        public void Shutdown()
        {
            if (!_initialized)
                return;
            foreach (var state in _terrain.Values)
                state.Dispose();
            _terrain.Clear();
            _textures?.Dispose();
            _textures = null;
            _normalGenerator = null;
            _initialized = false;
        }

        private void Remove(TerrainModuleIdentity identity)
        {
            if (!_terrain.TryGetValue(identity, out var state))
                return;
            _terrain.Remove(identity);
            state.Dispose();
        }
    }
}
