using System.Collections.Generic;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [CreateAssetMenu(
        fileName = "FoliageModule",
        menuName = "Saab/Map Streamer/Modules/Foliage")]
    public sealed class FoliageModuleDefinition : MapModuleDefinition
    {
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Shader foliageShader;
        [SerializeField] private Texture2D perlinNoise;
        [SerializeField] private Material downsampleMaterial;
        [SerializeField] private int layer;
        [SerializeField] private bool occlusion = true;
        [SerializeField] private bool disabled;
        [SerializeField] private bool debugPrintCount;
        [SerializeField] private bool debugNoDraw;
        [SerializeField] private bool nativeLeakDetection;
        [SerializeField] private long resourcePoolBytes =
            64L * 1024L * 1024L;
        [SerializeField] private List<FeatureSet> features =
            new List<FeatureSet>();

        public override string ModuleId => "terrain.foliage";

        public override bool TryValidate(out string failure)
        {
            if (!base.TryValidate(out failure))
                return false;
            if (computeShader == null || foliageShader == null)
            {
                failure = "Foliage requires compute and rendering shaders.";
                return false;
            }
            if (features == null)
            {
                failure = "Foliage feature collection is missing.";
                return false;
            }

            failure = null;
            return true;
        }

        public override IMapModule CreateRuntime(IMapModuleServices services) =>
            new FoliageModuleRuntime(
                CreateConfiguration(),
                services.Get<CameraControl>(),
                services.Get<IMapCoordinates>());

        internal FoliageModuleConfiguration CreateConfiguration() =>
            FoliageModuleConfiguration.Create(
                computeShader,
                foliageShader,
                perlinNoise,
                downsampleMaterial,
                layer,
                occlusion,
                disabled,
                debugPrintCount,
                debugNoDraw,
                nativeLeakDetection,
                resourcePoolBytes,
                features);
    }
}
