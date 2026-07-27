using GizmoSDK.Gizmo3D;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Configuration
{
    [CreateAssetMenu(
        fileName = "MapStreamerSettings",
        menuName = "Saab/Map Streamer/Streaming Settings")]
    public sealed class MapStreamerSettings : ScriptableObject
    {
        [SerializeField]
        private double maxBuildTime = 0.012;

        [SerializeField]
        private double minBuildTime = 0.004;

        [SerializeField]
        private byte dynamicLoaders = 4;

        [SerializeField]
        private IntersectMaskValue intersectMask =
            IntersectMaskValue.ALL;

        [SerializeField]
        private MapStreamerOptions options =
            MapStreamerOptions.RenderInUpdate;

        public double MaxBuildTime => maxBuildTime;
        public double MinBuildTime => minBuildTime;
        public byte DynamicLoaders => dynamicLoaders;
        public IntersectMaskValue IntersectMask => intersectMask;
        public MapStreamerOptions Options => options;

        internal MapStreamerRuntimeSettings CreateRuntimeSettings() =>
            new MapStreamerRuntimeSettings(
                maxBuildTime,
                minBuildTime,
                dynamicLoaders,
                intersectMask,
                options);
    }
}
