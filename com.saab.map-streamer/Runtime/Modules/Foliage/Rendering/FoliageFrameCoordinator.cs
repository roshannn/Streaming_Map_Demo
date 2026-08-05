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

namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageFrameCoordinator : IDisposable
    {
        private static class Property
        {
            public static readonly int Occlusion =
                Shader.PropertyToID("Occlusion");
            public static readonly int FrameCount =
                Shader.PropertyToID("FrameCount");
            public static readonly int DownscaleFactor =
                Shader.PropertyToID("DownscaleFactor");
        }

        private readonly FoliageModuleConfiguration _configuration;
        private readonly CameraControl _cameraControl;
        private readonly FoliageOcclusionPass _occlusion =
            new FoliageOcclusionPass();
        private readonly FoliageCullingPass _culling =
            new FoliageCullingPass();
        private readonly FoliageRenderPass _render =
            new FoliageRenderPass();
        private readonly FoliageDebugScope _debug;

        public FoliageFrameCoordinator(
            FoliageModuleConfiguration configuration,
            CameraControl cameraControl)
        {
            _configuration = configuration;
            _cameraControl = cameraControl;
            _debug = new FoliageDebugScope(
                configuration.NativeLeakDetection);
        }

        public void Render(IReadOnlyList<FoliageFeatureRuntime> features)
        {
            if (_configuration.Disabled)
                return;
            var camera = _cameraControl.Camera;
            camera.depthTextureMode |= DepthTextureMode.Depth;
            _configuration.ComputeShader.SetBool(
                Property.Occlusion,
                _configuration.Occlusion);
            _configuration.ComputeShader.SetInt(
                Property.FrameCount,
                UnityEngine.Time.frameCount);
            const int downscale = 4;
            var depth = _occlusion.Execute(
                downscale,
                _configuration.DownsampleMaterial);
            if (depth == null)
                return;
            _configuration.ComputeShader.SetInt(
                Property.DownscaleFactor,
                downscale);

            var maximumHeight = features.Count == 0
                ? 0f
                : features.Max(feature => feature.MaximumHeight);
            foreach (var feature in features)
            {
                var settings = GfxCaps.GetFoliageSettings(
                    feature.Configuration.SettingsType);
                feature.Settings = settings;
                if (!settings.Enabled || !feature.Enabled)
                    continue;
                if (feature.Material == null)
                {
                    Debug.LogError(
                        $"Foliage material for " +
                        $"{feature.Configuration.MapFeature} is missing.");
                    continue;
                }

                feature.DrawDistance =
                    FoliageCullingPass.CalculateDrawDistance(
                        camera,
                        feature.MaximumHeight,
                        feature.Configuration.ScreenCoverage) *
                    settings.DrawDistance;
                var points = feature.Placement.Cull(
                    _culling.GetFrustum(camera, feature.DrawDistance),
                    camera,
                    maximumHeight,
                    depth,
                    feature.Metadata,
                    feature.Configuration.ScreenCoverage);

                if (_configuration.DebugPrintCount)
                {
                    ComputeBuffer.CopyCount(points, feature.Indirect, 0);
                    var count = new int[4];
                    feature.Indirect.GetData(count);
                    Debug.LogWarning(
                        $"{feature.Configuration.FoliageSet.name} :: " +
                        $"{count[0]}/{points.count}");
                }

                if (_configuration.DebugNoDraw)
                    continue;
                _render.Draw(
                    feature.Material,
                    points,
                    feature.Indirect,
                    feature.DrawDistance,
                    camera.transform.position,
                    _configuration.Layer,
                    settings.Shadows);
            }
            _configuration.DebugPrintCount = false;
        }

        public void Dispose()
        {
            _occlusion.Dispose();
            _debug.Dispose();
        }
    }

}
