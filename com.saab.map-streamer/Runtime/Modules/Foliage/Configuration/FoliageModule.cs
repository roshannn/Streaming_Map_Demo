/*
 * Copyright (C) SAAB AB
 *
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 */

using System;
using System.Collections.Generic;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Utility.GfxCaps;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [Serializable]
    public sealed class FeatureSet
    {
        public SettingsFeatureType SettingsType;
        public MapFeature mapFeature;
        public FoliageSet FoliageSet;
        public bool Enabled;

        [Header("Main Settings")]
        public int BufferSize;

        [Range(0.001f, 1f)]
        public float ScreenCoverage = 0.001f;

        public float Density;

        /// <summary>
        /// Node LODs larger than this do not use this feature set.
        /// </summary>
        public uint NodeMaxWidth;

        public bool Shadows;
        public bool Crossboard;

        [Header("Calculated at runtime")]
        public float DrawDistance;

        internal FoliageFeatureConfiguration Snapshot() =>
            new FoliageFeatureConfiguration(
                SettingsType,
                mapFeature,
                FoliageSet,
                Enabled,
                BufferSize,
                ScreenCoverage,
                Density,
                NodeMaxWidth,
                Shadows,
                Crossboard);
    }

    /// <summary>
    /// Serialized compatibility data for scenes created before module profiles.
    /// Runtime behavior lives in <see cref="FoliageModuleRuntime"/>.
    /// </summary>
    [Obsolete(
        "Use FoliageModuleDefinition in a MapModuleProfile. " +
        "This component remains as a serialized compatibility facade.")]
    public sealed class FoliageModule : MonoBehaviour
    {
        public ComputeShader ComputeShader;
        public Shader FoliageShader;
        public Texture2D PerlinNoise;
        public int Layer;

        [Header("Debug Settings")]
        public bool DebugPrintCount;
        public bool Disabled;
        public bool DebugNoDraw;
        public bool NativeLeakDetection;
        public bool Occlusion = true;
        public Material DownsampleMaterial;

        [Min(0)]
        public long ResourcePoolBytes = 64L * 1024L * 1024L;

        [Header("Foliage Draw")]
        public List<FeatureSet> Features = new List<FeatureSet>();

        internal FoliageModuleConfiguration Snapshot() =>
            FoliageModuleConfiguration.Create(
                ComputeShader,
                FoliageShader,
                PerlinNoise,
                DownsampleMaterial,
                Layer,
                Occlusion,
                Disabled,
                DebugPrintCount,
                DebugNoDraw,
                NativeLeakDetection,
                ResourcePoolBytes,
                Features);
    }
}
