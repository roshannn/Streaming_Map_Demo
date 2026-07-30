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

    internal sealed class FoliageFeatureConfiguration
    {
        public FoliageFeatureConfiguration(
            SettingsFeatureType settingsType,
            MapFeature mapFeature,
            FoliageSet foliageSet,
            bool enabled,
            int bufferSize,
            float screenCoverage,
            float density,
            uint nodeMaxWidth,
            bool shadows,
            bool crossboard)
        {
            SettingsType = settingsType;
            MapFeature = mapFeature;
            FoliageSet = foliageSet;
            Enabled = enabled;
            BufferSize = bufferSize;
            ScreenCoverage = screenCoverage;
            Density = density;
            NodeMaxWidth = nodeMaxWidth;
            Shadows = shadows;
            Crossboard = crossboard;
        }

        public SettingsFeatureType SettingsType { get; }
        public MapFeature MapFeature { get; }
        public FoliageSet FoliageSet { get; }
        public bool Enabled { get; }
        public int BufferSize { get; }
        public float ScreenCoverage { get; }
        public float Density { get; }
        public uint NodeMaxWidth { get; }
        public bool Shadows { get; }
        public bool Crossboard { get; }
    }

}
