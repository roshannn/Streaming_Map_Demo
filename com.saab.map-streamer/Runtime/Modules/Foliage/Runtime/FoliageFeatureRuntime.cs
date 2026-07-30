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
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageFeatureRuntime : IDisposable
    {
        private static class MaterialProperty
        {
            public static readonly int IsToggled =
                Shader.PropertyToID("_isToggled");
            public static readonly int PerlinNoise =
                Shader.PropertyToID("_PerlinNoise");
            public static readonly int FoliageCount =
                Shader.PropertyToID("_foliageCount");
            public static readonly int MainTextureArray =
                Shader.PropertyToID("_MainTexArray");
            public static readonly int FoliageData =
                Shader.PropertyToID("_foliageData");
            public const string Crossboard = "CROSSBOARD_ON";
        }

        private FoliageFeatureRuntime(
            FoliageFeatureConfiguration configuration,
            SettingsFeature settings,
            FoliageFeature placement,
            Material material,
            Texture2DArray textures,
            ComputeBuffer metadata,
            ComputeBuffer indirect,
            float maximumHeight)
        {
            Configuration = configuration;
            Settings = settings;
            Placement = placement;
            Material = material;
            Textures = textures;
            Metadata = metadata;
            Indirect = indirect;
            MaximumHeight = maximumHeight;
            Enabled = settings.Enabled;
        }

        public FoliageFeatureConfiguration Configuration { get; }
        public SettingsFeature Settings { get; set; }
        public FoliageFeature Placement { get; private set; }
        public Material Material { get; private set; }
        public Texture2DArray Textures { get; private set; }
        public ComputeBuffer Metadata { get; private set; }
        public ComputeBuffer Indirect { get; private set; }
        public float MaximumHeight { get; }
        public float DrawDistance { get; set; }
        public bool Enabled { get; }

        public static FoliageFeatureRuntime Create(
            FoliageFeatureConfiguration configuration,
            SettingsFeature settings,
            FoliageModuleConfiguration module,
            FoliageAssetLibrary assets,
            int[] mapping,
            IMapCoordinates mapCoordinates)
        {
            FoliageFeature placement = null;
            Material material = null;
            Texture2DArray textures = null;
            ComputeBuffer metadata = null;
            ComputeBuffer indirect = null;
            try
            {
                placement = new FoliageFeature(
                    Mathf.CeilToInt(
                        configuration.BufferSize * settings.Density),
                    configuration.Density * settings.Density,
                    TerrainMapping.FeatureTruthTable(
                        mapping,
                        configuration.MapFeature),
                    module.ComputeShader,
                    mapCoordinates);

                material = new Material(module.FoliageShader);
                material.SetTexture(
                    MaterialProperty.PerlinNoise,
                    module.PerlinNoise);
                material.SetFloat(
                    MaterialProperty.IsToggled,
                    configuration.Crossboard ? 0f : 1f);
                if (configuration.Crossboard)
                    material.EnableKeyword(MaterialProperty.Crossboard);
                else
                    material.DisableKeyword(MaterialProperty.Crossboard);

#if UNITY_ANDROID
                var format = TextureFormat.ETC2_RGBA8;
#else
                var format = TextureFormat.DXT5;
#endif
                var foliage = configuration.FoliageSet.GetFoliageList;
                textures = assets.CreateTextureArray(foliage, format);
                material.SetInt(MaterialProperty.FoliageCount, foliage.Count);
                material.SetTexture(
                    MaterialProperty.MainTextureArray,
                    textures);

                var shaderData = new FoliageShaderData[foliage.Count];
                var maximumHeight = 0f;
                for (var index = 0; index < foliage.Count; ++index)
                {
                    var item = foliage[index];
                    maximumHeight = Mathf.Max(maximumHeight, item.MaxMin.y);
                    shaderData[index] = new FoliageShaderData
                    {
                        MaxMin = item.MaxMin,
                        Offset = item.Offset,
                        Weight = item.Weight
                    };
                }
                metadata = new ComputeBuffer(
                    foliage.Count,
                    sizeof(float) * 5);
                metadata.SetData(shaderData);
                material.SetBuffer(MaterialProperty.FoliageData, metadata);

                indirect = new ComputeBuffer(
                    4,
                    sizeof(uint),
                    ComputeBufferType.IndirectArguments);
                indirect.SetData(new uint[] { 0, 1, 0, 0 });

                return new FoliageFeatureRuntime(
                    configuration,
                    settings,
                    placement,
                    material,
                    textures,
                    metadata,
                    indirect,
                    maximumHeight);
            }
            catch
            {
                placement?.Dispose();
                metadata?.Release();
                indirect?.Release();
                if (material != null)
                    UnityEngine.Object.Destroy(material);
                if (textures != null)
                    UnityEngine.Object.Destroy(textures);
                throw;
            }
        }

        public void Dispose()
        {
            Placement?.Dispose();
            Metadata?.Release();
            Indirect?.Release();
            if (Material != null)
                UnityEngine.Object.Destroy(Material);
            if (Textures != null)
                UnityEngine.Object.Destroy(Textures);
            Placement = null;
            Metadata = null;
            Indirect = null;
            Material = null;
            Textures = null;
        }

        private struct FoliageShaderData
        {
            public Vector2 MaxMin;
            public Vector2 Offset;
            public float Weight;
        }
    }

}
