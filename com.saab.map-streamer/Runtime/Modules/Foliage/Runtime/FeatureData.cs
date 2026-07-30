using System;

using GizmoSDK.GizmoBase;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    public struct FeatureData : IDisposable
    {
        public TerrainModuleIdentity Identity { get; private set; }
        public GameObject Object { get; private set; }
        public ComputeBuffer PlacementMatrix { get; private set; }
        public Vector2 NodeOffset { get; private set; }
        public ComputeBuffer TerrainPoints;
        public Texture2D FeatureMap;
        public Texture2D Texture;
        public Texture surfaceHeight;
        public ComputeBuffer PixelToObject;

        public FeatureData(
            TerrainModuleIdentity identity,
            GameObject gameObject,
            Matrix3D matrix,
            float density,
            uint maxSide,
            float scale = 1000)
        {
            Identity = identity;
            Object = gameObject;

            var stepSize = (1 / density) * 10;
            PlacementMatrix = new ComputeBuffer(
                9,
                sizeof(float),
                ComputeBufferType.Default);
            var data = new[]
            {
                (float)matrix.v11,
                (float)matrix.v12,
                (float)(matrix.v13 % stepSize),
                (float)matrix.v21,
                (float)matrix.v22,
                (float)(matrix.v23 % stepSize),
                (float)matrix.v31,
                (float)matrix.v32,
                (float)matrix.v33
            };

            NodeOffset = new Vector2(
                (float)(matrix.v13 + matrix.v11) % scale,
                (float)(matrix.v23 + matrix.v22) % scale);
            PlacementMatrix.SetData(data);

            PixelToObject = null;
            surfaceHeight = null;
            Texture = null;
            TerrainPoints = null;
            FeatureMap = null;
        }

        public void Dispose()
        {
            if (surfaceHeight is RenderTexture renderTexture)
                renderTexture.Release();
            TerrainPoints?.Release();
            PlacementMatrix?.Release();
            PixelToObject?.Release();
        }
    }
}
