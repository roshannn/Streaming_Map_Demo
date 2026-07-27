using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Configuration
{
    internal readonly struct MapStreamerRuntimeSettings
    {
        public MapStreamerRuntimeSettings(
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

        public double MaxBuildTime { get; }
        public double MinBuildTime { get; }
        public byte DynamicLoaders { get; }
        public IntersectMaskValue IntersectMask { get; }
        public MapStreamerOptions Options { get; }

        public MapStreamerRuntimeSettings WithOption(
            MapStreamerOptions option) =>
            new MapStreamerRuntimeSettings(
                MaxBuildTime,
                MinBuildTime,
                DynamicLoaders,
                IntersectMask,
                Options | option);
    }
}
