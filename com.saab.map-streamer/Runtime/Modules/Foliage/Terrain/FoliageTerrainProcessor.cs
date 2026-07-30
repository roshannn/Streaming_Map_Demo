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

namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageTerrainProcessor
    {
        private static class Property
        {
            public static readonly int TerrainResolution =
                Shader.PropertyToID("terrainResolution");
            public static readonly int TerrainSize =
                Shader.PropertyToID("terrainSize");
            public static readonly int NodeOffset =
                Shader.PropertyToID("NodeOffset");
            public static readonly int Resolution =
                Shader.PropertyToID("Resolution");
            public static readonly int IndexCount =
                Shader.PropertyToID("indexCount");
            public static readonly int UvCount =
                Shader.PropertyToID("uvCount");
            public static readonly int IndexBuffer =
                Shader.PropertyToID("IndexBuffer");
            public static readonly int VertexBuffer =
                Shader.PropertyToID("VertexBuffer");
            public static readonly int MeshBoundsMax =
                Shader.PropertyToID("MeshBoundsMax");
            public static readonly int VertexBufferStride =
                Shader.PropertyToID("VertexBufferStride");
            public static readonly int TexcoordOffset =
                Shader.PropertyToID("TexcoordOffset");
            public static readonly int NormalOffset =
                Shader.PropertyToID("NormalOffset");
            public static readonly int PositionOffset =
                Shader.PropertyToID("PositionOffset");
            public static readonly int Texture =
                Shader.PropertyToID("Texture");
            public static readonly int SurfaceHeightMap =
                Shader.PropertyToID("SurfaceHeightMap");
            public static readonly int PixelToObjectCoord =
                Shader.PropertyToID("PixelToObjectCoord");
            public static readonly int Density =
                Shader.PropertyToID("Density");
        }

        private readonly ComputeShader _shader;
        private readonly IMapCoordinates _mapCoordinates;
        private readonly FoliageResourcePool _pool;
        private readonly Coordinate _coordinate = new Coordinate();

        public FoliageTerrainProcessor(
            ComputeShader shader,
            IMapCoordinates mapCoordinates,
            FoliageResourcePool pool)
        {
            _shader = shader;
            _mapCoordinates = mapCoordinates;
            _pool = pool;
        }

        public FoliageTerrainBuildScope Build(
            in TerrainModuleContext terrain,
            IReadOnlyList<FoliageFeatureRuntime> features,
            Func<SettingsFeatureType, SettingsFeature> getSettings)
        {
            if (terrain.IsAsset ||
                terrain.GameObject == null ||
                !terrain.GameObject.activeInHierarchy ||
                terrain.Mesh == null ||
                terrain.Texture == null ||
                terrain.FeatureTexture == null)
                return null;

            var info = terrain.NodeHandle.featureInfo;
            var pixelSize = new Vector2((float)info.v11, (float)info.v22);
            var textureSize = new Vector2(
                terrain.Texture.width,
                terrain.Texture.height);
            var nodeSide = Mathf.Max(
                textureSize.x * pixelSize.x,
                textureSize.y * pixelSize.y);
            if (nodeSide > 2048f)
                return null;

            _shader.SetVector(Property.TerrainResolution, textureSize);
            _shader.SetVector(Property.TerrainSize, terrain.Mesh.bounds.size);
            _shader.SetVector(
                Property.NodeOffset,
                new Vector2(
                    (float)(info.v13 + info.v11) % 1000f,
                    (float)(info.v23 + info.v22) % 1000f));
            _shader.SetVector(Property.Resolution, pixelSize);

            var center = terrain.NodeHandle.node.BoundaryCenter;
            if (!_mapCoordinates.TryGlobalToWorld(
                    center,
                    out CartPos cartesian))
                return null;
            _coordinate.SetCartPos(cartesian);
            _coordinate.GetUTMPos(out var utm);
            var topLeft = info * new Vec3D(0, 0, 1);
            var offset = new Vec3D(
                topLeft.x - utm.Easting,
                0,
                topLeft.y - utm.Northing);

            ComputeBuffer pixelToWorld = null;
            RenderTexture generatedSurface = null;
            var added = new List<FoliageFeatureRuntime>();
            try
            {
                pixelToWorld = GeneratePixelToWorld(
                    textureSize,
                    pixelSize,
                    terrain.Mesh,
                    offset);
                if (terrain.NodeHandle.surfaceHeight == null)
                    generatedSurface = GenerateSurfaceHeight(terrain.Texture);

                foreach (var feature in features)
                {
                    if (!feature.Enabled ||
                        nodeSide >= feature.Configuration.NodeMaxWidth)
                        continue;
                    var settings = getSettings(
                        feature.Configuration.SettingsType);
                    _shader.SetFloat(
                        Property.Density,
                        feature.Configuration.Density * settings.Density);
                    if (feature.Placement.AddFoliage(
                            terrain.Identity,
                            terrain.GameObject,
                            terrain.NodeHandle,
                            pixelToWorld,
                            generatedSurface))
                        added.Add(feature);
                }

                return new FoliageTerrainBuildScope(
                    new FoliageTerrainState(
                        terrain.Identity,
                        added));
            }
            catch
            {
                foreach (var feature in added)
                    feature.Placement.RemoveFoliage(terrain.Identity);
                throw;
            }
            finally
            {
                _pool.Return(pixelToWorld);
                if (generatedSurface != null)
                {
                    generatedSurface.Release();
                    UnityEngine.Object.Destroy(generatedSurface);
                }
            }
        }

        private RenderTexture GenerateSurfaceHeight(Texture texture)
        {
            var result = new RenderTexture(
                texture.width,
                texture.height,
                24,
                RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                name = "Foliage Surface Height"
            };
            result.Create();
            try
            {
                var kernel = _shader.FindKernel("CSSurfaceHeightMap");
                _shader.SetTexture(kernel, Property.Texture, texture);
                _shader.SetTexture(
                    kernel,
                    Property.SurfaceHeightMap,
                    result);
                _shader.Dispatch(
                    kernel,
                    Mathf.Max(1, Mathf.CeilToInt(texture.width / 8f)),
                    Mathf.Max(1, Mathf.CeilToInt(texture.height / 8f)),
                    1);
                return result;
            }
            catch
            {
                result.Release();
                UnityEngine.Object.Destroy(result);
                throw;
            }
        }

        private ComputeBuffer GeneratePixelToWorld(
            Vector2 textureSize,
            Vector2 pixelSize,
            Mesh mesh,
            Vec3D offset)
        {
            GraphicsBuffer vertex = null;
            GraphicsBuffer index = null;
            GraphicsBuffer indexCopy = null;
            ComputeBuffer result = null;
            try
            {
                _shader.SetVector(Property.Resolution, pixelSize);
                mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
                mesh.indexBufferTarget |= GraphicsBuffer.Target.CopySource;
                vertex = mesh.GetVertexBuffer(0);
                index = mesh.GetIndexBuffer();
                indexCopy = CreateIndexBufferCopy(mesh);

                _shader.SetInt(
                    Property.PositionOffset,
                    mesh.GetVertexAttributeOffset(VertexAttribute.Position));
                _shader.SetInt(
                    Property.TexcoordOffset,
                    mesh.GetVertexAttributeOffset(VertexAttribute.TexCoord0));
                _shader.SetInt(
                    Property.NormalOffset,
                    mesh.GetVertexAttributeOffset(VertexAttribute.Normal));
                _shader.SetInt(
                    Property.VertexBufferStride,
                    mesh.GetVertexBufferStride(0));
                _shader.SetInt(Property.UvCount, vertex.count);
                _shader.SetVector(
                    Property.MeshBoundsMax,
                    mesh.bounds.center -
                    new Vector3(
                        (float)-offset.x,
                        (float)offset.y,
                        (float)offset.z));

                var indexCount = (int)mesh.GetIndexCount(0);
                var triangleCount = Mathf.CeilToInt(indexCount / 3f);
                var kernel = _shader.FindKernel("CSPixelToObject");
                result = _pool.Rent(
                    Mathf.CeilToInt(textureSize.x * textureSize.y),
                    sizeof(float) * 3);
                _shader.SetInt(Property.IndexCount, triangleCount);
                _shader.SetBuffer(kernel, Property.VertexBuffer, vertex);
                _shader.SetBuffer(kernel, Property.IndexBuffer, indexCopy);
                _shader.SetBuffer(
                    kernel,
                    Property.PixelToObjectCoord,
                    result);
                _shader.Dispatch(
                    kernel,
                    Mathf.Max(1, Mathf.CeilToInt(triangleCount / 4f)),
                    1,
                    1);
                return result;
            }
            catch
            {
                _pool.Return(result);
                throw;
            }
            finally
            {
                indexCopy?.Release();
                index?.Release();
                vertex?.Release();
            }
        }

        internal static GraphicsBuffer CreateIndexBufferCopy(Mesh mesh)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            var indices = mesh.GetIndices(0);
            if (mesh.indexFormat == IndexFormat.UInt16)
            {
                var packed = new uint[(indices.Length + 1) / 2];
                for (var index = 0; index < indices.Length; index += 2)
                {
                    var low = (uint)(indices[index] & 0xffff);
                    var high = index + 1 < indices.Length
                        ? (uint)(indices[index + 1] & 0xffff) << 16
                        : 0u;
                    packed[index / 2] = low | high;
                }
                var result = new GraphicsBuffer(
                    GraphicsBuffer.Target.Raw,
                    packed.Length,
                    sizeof(uint));
                result.SetData(packed);
                return result;
            }

            var unpacked = new uint[indices.Length];
            for (var index = 0; index < indices.Length; ++index)
                unpacked[index] = (uint)indices[index];
            var copy = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                unpacked.Length,
                sizeof(uint));
            copy.SetData(unpacked);
            return copy;
        }
    }

}
