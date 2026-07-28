using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    public readonly struct StreamingFrame
    {
        public StreamingFrame(
            Matrix4x4 worldToCameraMatrix,
            double globalX,
            double globalY,
            double globalZ,
            float verticalFieldOfView,
            float aspect,
            float nearClipPlane,
            float farClipPlane,
            float lodFactor,
            double renderTime)
        {
            WorldToCameraMatrix = worldToCameraMatrix;
            GlobalX = globalX;
            GlobalY = globalY;
            GlobalZ = globalZ;
            VerticalFieldOfView = verticalFieldOfView;
            Aspect = aspect;
            NearClipPlane = nearClipPlane;
            FarClipPlane = farClipPlane;
            LodFactor = lodFactor;
            RenderTime = renderTime;
        }

        public Matrix4x4 WorldToCameraMatrix { get; }
        public double GlobalX { get; }
        public double GlobalY { get; }
        public double GlobalZ { get; }
        public float VerticalFieldOfView { get; }
        public float Aspect { get; }
        public float NearClipPlane { get; }
        public float FarClipPlane { get; }
        public float LodFactor { get; }
        public double RenderTime { get; }
    }
}
