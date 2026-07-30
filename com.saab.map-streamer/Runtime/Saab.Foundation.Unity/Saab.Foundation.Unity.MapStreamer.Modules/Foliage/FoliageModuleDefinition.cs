using System;
using System.Collections.Generic;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [CreateAssetMenu(
        fileName = "FoliageModule",
        menuName = "Saab/Map Streamer/Modules/Foliage")]
    public sealed class FoliageModuleDefinition : MapModuleDefinition
    {
        [SerializeField]
        private ComputeShader computeShader;

        [SerializeField]
        private Shader foliageShader;

        [SerializeField]
        private Texture2D perlinNoise;

        [SerializeField]
        private Material downsampleMaterial;

        [SerializeField]
        private int layer;

        [SerializeField]
        private bool occlusion = true;

        [SerializeField]
        private bool disabled;

        [SerializeField]
        private List<FeatureSet> features = new List<FeatureSet>();

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

        public override IMapModule CreateRuntime(
            IMapModuleServices services) =>
            new FoliageModuleRuntime(
                this,
                services.Get<FoliageModule>());

        internal void ApplyTo(FoliageModule module)
        {
            module.ComputeShader = computeShader;
            module.FoliageShader = foliageShader;
            module.PerlinNoise = perlinNoise;
            module.DownsampleMaterial = downsampleMaterial;
            module.Layer = layer;
            module.Occlusion = occlusion;
            module.Disabled = disabled;
            module.Features = new List<FeatureSet>(features.Count);
            foreach (var feature in features)
                module.Features.Add(feature?.CloneConfiguration());
        }
    }

    internal sealed class FoliageModuleRuntime :
        IMapModule,
        IMapEventHandler<TerrainAddedEvent>,
        IMapEventHandler<TerrainRemovedEvent>,
        IMapEventHandler<StreamingFrameCompletedEvent>
    {
        private readonly FoliageModuleDefinition _definition;
        private readonly FoliageModule _compatibility;

        public FoliageModuleRuntime(
            FoliageModuleDefinition definition,
            FoliageModule compatibility)
        {
            _definition = definition;
            _compatibility = compatibility;
        }

        public void Initialize()
        {
            _definition.ApplyTo(_compatibility);
            _compatibility.Initialize();
        }
        public void Shutdown() => _compatibility.Shutdown();

        public void Handle(in TerrainAddedEvent mapEvent)
        {
            var terrain = mapEvent.Terrain;
            _compatibility.OnTerrainAdded(in terrain);
        }

        public void Handle(in TerrainRemovedEvent mapEvent)
        {
            var terrain = mapEvent.Terrain;
            _compatibility.OnTerrainRemoved(in terrain);
        }

        public void Handle(in StreamingFrameCompletedEvent mapEvent)
        {
            var frame = mapEvent.Frame;
            _compatibility.OnStreamingFrameCompleted(in frame);
        }
    }
}
