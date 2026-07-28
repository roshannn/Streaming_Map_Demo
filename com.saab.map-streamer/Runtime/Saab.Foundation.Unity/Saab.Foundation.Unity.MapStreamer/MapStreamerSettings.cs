using System;

using GizmoSDK.Gizmo3D;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
    [Flags]
    public enum MapStreamerOptions
    {
        None = 0,
        RenderInUpdate = 1 << 0,
        DisableInstancing = 1 << 1,
        LazyLoadAssets = 1 << 2,
    }

    [CreateAssetMenu(
        fileName = "MapStreamerSettings",
        menuName = "Saab/Map Streamer/Settings")]
    public sealed class MapStreamerSettings : ScriptableObject
    {
        [SerializeField]
        private double maxBuildTime = 0.012;

        [SerializeField]
        private double minBuildTime = 0.004;

        [SerializeField]
        private byte dynamicLoaders = 4;

        [SerializeField]
        private IntersectMaskValue intersectMask = IntersectMaskValue.ALL;

        [SerializeField]
        private MapStreamerOptions options = MapStreamerOptions.RenderInUpdate;

        public RuntimeMapStreamerSettings CreateRuntimeCopy()
        {
            return new RuntimeMapStreamerSettings(
                maxBuildTime,
                minBuildTime,
                dynamicLoaders,
                intersectMask,
                options);
        }
    }

    public sealed class RuntimeMapStreamerSettings
    {
        public RuntimeMapStreamerSettings(
            double maxBuildTime,
            double minBuildTime,
            byte dynamicLoaders,
            IntersectMaskValue intersectMask,
            MapStreamerOptions options)
        {
            MaxBuildTime = maxBuildTime;
            MinBuildTime = minBuildTime;
            DynamicLoaders = dynamicLoaders;
            IntersectMask = intersectMask;
            Options = options;
        }

        public double MaxBuildTime { get; set; }
        public double MinBuildTime { get; set; }
        public byte DynamicLoaders { get; set; }
        public IntersectMaskValue IntersectMask { get; set; }
        public MapStreamerOptions Options { get; set; }
    }
}
