using System;
using System.Collections.Generic;
using System.Linq;

using Saab.Utility.GfxCaps;
using Saab.Foundation.Unity.MapStreamer.Utils;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Rendering
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Runtime;

    internal sealed class TerrainMaterialBinder
    {
        private static readonly int Textures =
            Shader.PropertyToID("_Textures");
        private static readonly int NormalMaps =
            Shader.PropertyToID("_NormalMaps");
        private static readonly int MappingBuffer =
            Shader.PropertyToID("_MappingBuffer");
        private static readonly int NormalBuffer =
            Shader.PropertyToID("_NormalBuffer");
        private static readonly int WaterIndex =
            Shader.PropertyToID("_WaterIndex");

        public TerrainShadingState Bind(
            in TerrainModuleContext terrain,
            TerrainTextureLibrary textures,
            TerrainNormalGenerator normalGenerator)
        {
            var source = terrain.Renderer.sharedMaterial;
            if (source == null)
                return null;

            var material = new Material(source)
            {
                name = source.name + " (Terrain Shading)"
            };
            material.SetTexture(Textures, textures.Albedo);
            material.SetTexture(NormalMaps, textures.Normals);
            material.SetBuffer(MappingBuffer, textures.Mapping);

            var normalBuffer = normalGenerator.Generate(terrain.Mesh);
            if (normalBuffer != null)
                material.SetBuffer(NormalBuffer, normalBuffer);

            if (TerrainMapping.TryFindSourceLabel(
                    MapFeature.Water,
                    out var waterLabel))
                material.SetInt(WaterIndex, waterLabel);

            terrain.Renderer.sharedMaterial = material;
            return new TerrainShadingState(
                terrain.Renderer,
                source,
                material,
                normalBuffer);
        }
    }

}
