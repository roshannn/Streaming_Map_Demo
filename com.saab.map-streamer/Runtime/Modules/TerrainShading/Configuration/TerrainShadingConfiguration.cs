using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Configuration
{
    internal sealed class TerrainShadingConfiguration
    {
        public TerrainShadingConfiguration(
            bool enableDetailedTextures,
            float hueShiftInclusion,
            Color targetHue,
            TerrainDetailTextureAssetSet detailTextureSet,
            ComputeShader normalComputeShader)
        {
            EnableDetailedTextures = enableDetailedTextures;
            HueShiftInclusion = hueShiftInclusion;
            TargetHue = targetHue;
            DetailTextureSet = detailTextureSet;
            NormalComputeShader = normalComputeShader;
        }

        public bool EnableDetailedTextures { get; }
        public float HueShiftInclusion { get; }
        public Color TargetHue { get; }
        public TerrainDetailTextureAssetSet DetailTextureSet { get; }
        public ComputeShader NormalComputeShader { get; }
    }
}
