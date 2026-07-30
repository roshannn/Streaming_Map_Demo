using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    internal sealed class FoliageAssetLibrary
    {
        public Texture2DArray CreateTextureArray(
            IReadOnlyList<Foliage> foliage,
            TextureFormat format)
        {
            if (foliage == null || foliage.Count == 0)
                throw new ArgumentException(
                    "At least one foliage asset is required.",
                    nameof(foliage));

            var resolution = Mathf.Max(
                foliage.Max(value => value.MainTexture.width),
                foliage.Max(value => value.MainTexture.height));
            resolution = Mathf.NextPowerOfTwo(resolution);
            var textureArray = new Texture2DArray(
                resolution,
                resolution,
                foliage.Count,
                format,
                true)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            var renderTexture = new RenderTexture(
                resolution,
                resolution,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                useMipMap = true,
                antiAliasing = 1,
                name = "Foliage Asset Library Staging"
            };

            var previous = RenderTexture.active;
            try
            {
                for (var index = 0; index < foliage.Count; index++)
                {
                    Graphics.Blit(
                        foliage[index].MainTexture,
                        renderTexture);
                    RenderTexture.active = renderTexture;
                    var staging = new Texture2D(
                        resolution,
                        resolution,
                        TextureFormat.ARGB32,
                        true);
                    try
                    {
                        staging.ReadPixels(
                            new Rect(0, 0, resolution, resolution),
                            0,
                            0);
                        staging.Apply(true);
                        staging.Compress(true);
                        Graphics.CopyTexture(
                            staging,
                            0,
                            textureArray,
                            index);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(staging);
                    }
                }

                textureArray.Apply(false, true);
                return textureArray;
            }
            catch
            {
                UnityEngine.Object.Destroy(textureArray);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }
    }

    internal sealed class FoliageTerrainState : IDisposable
    {
        public FoliageTerrainState(TerrainModuleIdentity identity)
        {
            Identity = identity;
        }

        public TerrainModuleIdentity Identity { get; }
        public ComputeBuffer PixelToWorld { get; set; }
        public RenderTexture SurfaceHeight { get; set; }
        public GraphicsBuffer VertexBuffer { get; set; }
        public GraphicsBuffer IndexBuffer { get; set; }
        public GraphicsBuffer IndexCopy { get; set; }

        public void Dispose()
        {
            PixelToWorld?.Release();
            SurfaceHeight?.Release();
            VertexBuffer?.Release();
            IndexBuffer?.Release();
            IndexCopy?.Release();
            PixelToWorld = null;
            SurfaceHeight = null;
            VertexBuffer = null;
            IndexBuffer = null;
            IndexCopy = null;
        }
    }

    internal sealed class FoliageResourcePool : IDisposable
    {
        private readonly long _maximumBytes;
        private readonly Stack<ComputeBuffer> _buffers =
            new Stack<ComputeBuffer>();
        private long _pooledBytes;

        public FoliageResourcePool(long maximumBytes)
        {
            _maximumBytes = Math.Max(0, maximumBytes);
        }

        public ComputeBuffer Rent(int count, int stride)
        {
            while (_buffers.Count > 0)
            {
                var candidate = _buffers.Pop();
                _pooledBytes -= (long)candidate.count * candidate.stride;
                if (candidate.count == count &&
                    candidate.stride == stride)
                    return candidate;
                candidate.Release();
            }

            return new ComputeBuffer(count, stride);
        }

        public void Return(ComputeBuffer buffer)
        {
            if (buffer == null)
                return;
            var bytes = (long)buffer.count * buffer.stride;
            if (_pooledBytes + bytes > _maximumBytes)
            {
                buffer.Release();
                return;
            }

            _buffers.Push(buffer);
            _pooledBytes += bytes;
        }

        public void Dispose()
        {
            while (_buffers.Count > 0)
                _buffers.Pop().Release();
            _pooledBytes = 0;
        }
    }

    internal sealed class FoliageOcclusionPass : IDisposable
    {
        private RenderTexture _depth;

        public bool IsDepthAvailable =>
            Shader.GetGlobalTexture("_CameraDepthTexture") != null;

        public RenderTexture Execute(
            int downscale,
            Material downsampleMaterial)
        {
            if (!IsDepthAvailable || downsampleMaterial == null)
                return null;

            var width = Mathf.Max(1, Screen.width / downscale);
            var height = Mathf.Max(1, Screen.height / downscale);
            if (_depth == null ||
                _depth.width != width ||
                _depth.height != height)
            {
                Dispose();
                _depth = new RenderTexture(
                    width,
                    height,
                    0,
                    RenderTextureFormat.RFloat)
                {
                    name = "Foliage Occlusion Depth",
                    filterMode = FilterMode.Point,
                    useMipMap = false
                };
                _depth.Create();
            }

            Graphics.Blit(null, _depth, downsampleMaterial);
            downsampleMaterial.mainTexture = _depth;
            return _depth;
        }

        public void Dispose()
        {
            if (_depth == null)
                return;
            _depth.Release();
            UnityEngine.Object.Destroy(_depth);
            _depth = null;
        }
    }

    internal sealed class FoliageCullingPass
    {
        private readonly Plane[] _planes = new Plane[6];
        private readonly Vector4[] _shaderPlanes = new Vector4[6];

        public Vector4[] GetFrustum(Camera camera, float drawDistance)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            for (var index = 0; index < _planes.Length; index++)
            {
                var normal = _planes[index].normal;
                _shaderPlanes[index] = new Vector4(
                    normal.x,
                    normal.y,
                    normal.z,
                    _planes[index].distance);
            }
            _shaderPlanes[5].w = drawDistance;
            return _shaderPlanes;
        }

        public static float CalculateDrawDistance(
            Camera camera,
            float objectHeight,
            float coverage)
        {
            var halfFov =
                camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            return objectHeight /
                (2.0f * Mathf.Tan(halfFov) * coverage);
        }
    }

    internal sealed class FoliageRenderPass
    {
        public void Draw(
            Material material,
            ComputeBuffer points,
            ComputeBuffer indirect,
            float drawDistance,
            int layer,
            bool shadows)
        {
            material.SetBuffer("_PointBuffer", points);
            ComputeBuffer.CopyCount(points, indirect, 0);
            var size = new Vector3(
                drawDistance,
                drawDistance,
                drawDistance);
            Graphics.DrawProceduralIndirect(
                material,
                new Bounds(Vector3.zero, size),
                MeshTopology.Points,
                indirect,
                0,
                null,
                null,
                shadows
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off,
                true,
                layer);
        }
    }
}
