using System;
using System.Collections.Generic;
using System.Linq;

using Saab.Utility.GfxCaps;
using Saab.Foundation.Unity.MapStreamer.Utils;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    internal sealed class TerrainTextureLibrary : IDisposable
    {
        public Texture2DArray Albedo { get; private set; }
        public Texture2DArray Normals { get; private set; }
        public ComputeBuffer Mapping { get; private set; }

        public bool Build(TerrainDetailTextureAssetSet textureSet)
        {
            if (textureSet == null ||
                textureSet.Textures == null ||
                textureSet.Textures.Count == 0)
                return false;

            var featureMapping = TerrainMapping.MapFeatureData();
            var resolvedMapping = new int[256];
            for (var index = 0; index < textureSet.Textures.Count; index++)
            {
                var mappedTexture = textureSet.Textures[index];
                var matches = TerrainMapping.FeatureTruthTable(
                    featureMapping,
                    mappedTexture.Mapping);
                for (var feature = 0; feature < matches.Length; feature++)
                {
                    if (matches[feature] == 1)
                        resolvedMapping[feature] = index + 1;
                }
            }

#if UNITY_ANDROID
            var format = TextureFormat.ETC2_RGBA8;
#else
            var format = TextureFormat.DXT5;
#endif
            var albedo = textureSet.Textures
                .Select(value => value.Asset != null
                    ? value.Asset.Albedo
                    : null)
                .ToList();
            var normals = textureSet.Textures
                .Select(value => value.Asset != null
                    ? value.Asset.Normal
                    : null)
                .ToList();
            if (albedo.Any(value => value == null) ||
                normals.Any(value => value == null))
                return false;

            Albedo = TextureUtility.Create2DArray(albedo, format);
            Normals = TextureUtility.Create2DArray(normals, format);
            Mapping = new ComputeBuffer(resolvedMapping.Length, sizeof(int));
            Mapping.SetData(resolvedMapping);
            return true;
        }

        public void Dispose()
        {
            Mapping?.Release();
            Mapping = null;
            if (Albedo != null)
                UnityEngine.Object.Destroy(Albedo);
            if (Normals != null)
                UnityEngine.Object.Destroy(Normals);
            Albedo = null;
            Normals = null;
        }
    }

    internal sealed class TerrainNormalGenerator
    {
        private readonly ComputeShader _shader;

        public TerrainNormalGenerator(ComputeShader shader)
        {
            _shader = shader;
        }

        public ComputeBuffer Generate(Mesh mesh)
        {
            if (_shader == null || mesh == null)
                return null;

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices.Length == 0 || triangles.Length == 0)
                return null;

            using (var vertexBuffer = new ComputeBuffer(
                       vertices.Length,
                       sizeof(float) * 3))
            using (var indexBuffer = new ComputeBuffer(
                       triangles.Length / 3,
                       sizeof(int) * 3))
            {
                var normals = new ComputeBuffer(
                    vertices.Length,
                    sizeof(float) * 3);
                vertexBuffer.SetData(vertices);
                indexBuffer.SetData(triangles);
                _shader.SetBuffer(0, "vertexPositions", vertexBuffer);
                _shader.SetBuffer(0, "triangleIndices", indexBuffer);
                _shader.SetBuffer(0, "vertexNormals", normals);
                _shader.SetInt("numVertices", vertices.Length);
                _shader.SetInt(
                    "triangleIndicesLength",
                    triangles.Length);
                _shader.Dispatch(
                    0,
                    Mathf.CeilToInt(vertices.Length / 64.0f),
                    1,
                    1);
                return normals;
            }
        }
    }

    internal sealed class TerrainMaterialBinder
    {
        private static readonly int Textures =
            Shader.PropertyToID("_Textures");
        private static readonly int NormalMaps =
            Shader.PropertyToID("_NormalMaps");
        private static readonly int MappingBuffer =
            Shader.PropertyToID("_MappingBuffer");
        private static readonly int NormalBuffer =
            Shader.PropertyToID("_NormalBuffer");
        private static readonly int WaterIndex =
            Shader.PropertyToID("_WaterIndex");

        public TerrainShadingState Bind(
            in TerrainModuleContext terrain,
            TerrainTextureLibrary textures,
            TerrainNormalGenerator normalGenerator)
        {
            var source = terrain.Renderer.sharedMaterial;
            if (source == null)
                return null;

            var material = new Material(source)
            {
                name = source.name + " (Terrain Shading)"
            };
            material.SetTexture(Textures, textures.Albedo);
            material.SetTexture(NormalMaps, textures.Normals);
            material.SetBuffer(MappingBuffer, textures.Mapping);

            var normalBuffer = normalGenerator.Generate(terrain.Mesh);
            if (normalBuffer != null)
                material.SetBuffer(NormalBuffer, normalBuffer);

            if (TerrainMapping.TryFindSourceLabel(
                    MapFeature.Water,
                    out var waterLabel))
                material.SetInt(WaterIndex, waterLabel);

            terrain.Renderer.sharedMaterial = material;
            return new TerrainShadingState(
                terrain.Renderer,
                source,
                material,
                normalBuffer);
        }
    }

    internal sealed class TerrainShadingState : IDisposable
    {
        private readonly MeshRenderer _renderer;
        private readonly Material _original;
        private Material _owned;
        private ComputeBuffer _normalBuffer;

        public TerrainShadingState(
            MeshRenderer renderer,
            Material original,
            Material owned,
            ComputeBuffer normalBuffer)
        {
            _renderer = renderer;
            _original = original;
            _owned = owned;
            _normalBuffer = normalBuffer;
        }

        public void Dispose()
        {
            if (_renderer != null &&
                _renderer.sharedMaterial == _owned)
                _renderer.sharedMaterial = _original;
            _normalBuffer?.Release();
            _normalBuffer = null;
            if (_owned != null)
                UnityEngine.Object.Destroy(_owned);
            _owned = null;
        }
    }

    internal sealed class TerrainShadingModuleRuntime :
        IMapModule,
        IMapEventHandler<TerrainAddedEvent>,
        IMapEventHandler<TerrainRemovedEvent>
    {
        private readonly TerrainShadingModuleDefinition _definition;
        private readonly Dictionary<
            TerrainModuleIdentity,
            TerrainShadingState> _terrain =
                new Dictionary<TerrainModuleIdentity, TerrainShadingState>();
        private readonly TerrainMaterialBinder _binder =
            new TerrainMaterialBinder();

        private TerrainTextureLibrary _textures;
        private TerrainNormalGenerator _normalGenerator;
        private bool _initialized;

        public TerrainShadingModuleRuntime(
            TerrainShadingModuleDefinition definition)
        {
            _definition = definition;
        }

        public void Initialize()
        {
            if (_initialized)
                return;

            Shader.SetGlobalColor(
                "_TargetTerrainColor",
                _definition.TargetHue);
            Shader.SetGlobalFloat(
                "_HueShift",
                _definition.HueShiftInclusion);

            var enabled = _definition.EnableDetailedTextures &&
                GfxCaps.CurrentCaps.HasFlag(
                    Capability.UseTerrainDetailTextures);
            _textures = new TerrainTextureLibrary();
            if (enabled && !_textures.Build(_definition.DetailTextureSet))
                throw new InvalidOperationException(
                    "Terrain detail texture library could not be built.");
            _normalGenerator = new TerrainNormalGenerator(
                _definition.NormalComputeShader);
            _initialized = true;
        }

        public void Handle(in TerrainAddedEvent mapEvent)
        {
            if (!_initialized || _textures.Albedo == null)
                return;

            var terrain = mapEvent.Terrain;
            if (!terrain.FeatureTexture || !terrain.Texture)
                return;

            Remove(terrain.Identity);
            var state = _binder.Bind(
                in terrain,
                _textures,
                _normalGenerator);
            if (state != null)
                _terrain.Add(terrain.Identity, state);
        }

        public void Handle(in TerrainRemovedEvent mapEvent) =>
            Remove(mapEvent.Terrain.Identity);

        public void Shutdown()
        {
            if (!_initialized)
                return;
            foreach (var state in _terrain.Values)
                state.Dispose();
            _terrain.Clear();
            _textures?.Dispose();
            _textures = null;
            _normalGenerator = null;
            _initialized = false;
        }

        private void Remove(TerrainModuleIdentity identity)
        {
            if (!_terrain.TryGetValue(identity, out var state))
                return;
            _terrain.Remove(identity);
            state.Dispose();
        }
    }
}
