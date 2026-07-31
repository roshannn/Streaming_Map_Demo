using System;

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
}
