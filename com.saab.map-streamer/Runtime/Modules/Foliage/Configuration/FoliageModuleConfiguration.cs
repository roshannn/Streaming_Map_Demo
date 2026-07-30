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

namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageModuleConfiguration
    {
        private FoliageModuleConfiguration(
            ComputeShader computeShader,
            Shader foliageShader,
            Texture2D perlinNoise,
            Material downsampleMaterial,
            int layer,
            bool occlusion,
            bool disabled,
            bool debugPrintCount,
            bool debugNoDraw,
            bool nativeLeakDetection,
            long resourcePoolBytes,
            IReadOnlyList<FoliageFeatureConfiguration> features)
        {
            ComputeShader = computeShader;
            FoliageShader = foliageShader;
            PerlinNoise = perlinNoise;
            DownsampleMaterial = downsampleMaterial;
            Layer = layer;
            Occlusion = occlusion;
            Disabled = disabled;
            DebugPrintCount = debugPrintCount;
            DebugNoDraw = debugNoDraw;
            NativeLeakDetection = nativeLeakDetection;
            ResourcePoolBytes = Math.Max(0, resourcePoolBytes);
            Features = features;
        }

        public ComputeShader ComputeShader { get; }
        public Shader FoliageShader { get; }
        public Texture2D PerlinNoise { get; }
        public Material DownsampleMaterial { get; }
        public int Layer { get; }
        public bool Occlusion { get; }
        public bool Disabled { get; }
        public bool DebugPrintCount { get; set; }
        public bool DebugNoDraw { get; }
        public bool NativeLeakDetection { get; }
        public long ResourcePoolBytes { get; }
        public IReadOnlyList<FoliageFeatureConfiguration> Features { get; }

        public static FoliageModuleConfiguration Create(
            ComputeShader computeShader,
            Shader foliageShader,
            Texture2D perlinNoise,
            Material downsampleMaterial,
            int layer,
            bool occlusion,
            bool disabled,
            bool debugPrintCount,
            bool debugNoDraw,
            bool nativeLeakDetection,
            long resourcePoolBytes,
            IEnumerable<FeatureSet> features)
        {
            var snapshots = features == null
                ? Array.Empty<FoliageFeatureConfiguration>()
                : features.Where(value => value != null)
                    .Select(value => value.Snapshot())
                    .ToArray();
            return new FoliageModuleConfiguration(
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
                snapshots);
        }

        public void Validate()
        {
            if (ComputeShader == null || FoliageShader == null)
                throw new InvalidOperationException(
                    "Foliage requires compute and rendering shaders.");
            foreach (var feature in Features)
            {
                if (feature.FoliageSet == null)
                    throw new InvalidOperationException(
                        $"Foliage feature {feature.MapFeature} has no asset set.");
            }
        }
    }

}
