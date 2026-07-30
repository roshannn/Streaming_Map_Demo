using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Runtime;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [CreateAssetMenu(
        fileName = "TerrainShadingModule",
        menuName = "Saab/Map Streamer/Modules/Terrain Shading")]
    public sealed class TerrainShadingModuleDefinition :
        MapModuleDefinition
    {
        [SerializeField]
        private bool enableDetailedTextures = true;

        [SerializeField, Range(0, 1)]
        private float hueShiftInclusion = 0.4f;

        [SerializeField]
        private Color targetHue =
            new Color(70f / 256f, 140f / 256f, 70f / 256f);

        [SerializeField]
        private TerrainDetailTextureAssetSet detailTextureSet;

        [SerializeField]
        private ComputeShader normalComputeShader;

        public override string ModuleId => "terrain.shading";
        public bool EnableDetailedTextures => enableDetailedTextures;
        public float HueShiftInclusion => hueShiftInclusion;
        public Color TargetHue => targetHue;
        public TerrainDetailTextureAssetSet DetailTextureSet =>
            detailTextureSet;
        public ComputeShader NormalComputeShader => normalComputeShader;

        public override bool TryValidate(out string failure)
        {
            if (!base.TryValidate(out failure))
                return false;
            if (enableDetailedTextures && detailTextureSet == null)
            {
                failure =
                    "Terrain shading requires a detail texture set when enabled.";
                return false;
            }

            failure = null;
            return true;
        }

        public override IMapModule CreateRuntime(
            IMapModuleServices services) =>
            new TerrainShadingModuleRuntime(this);
    }
}
