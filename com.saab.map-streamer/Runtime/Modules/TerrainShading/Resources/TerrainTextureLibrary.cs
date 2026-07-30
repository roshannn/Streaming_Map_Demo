using System;
using System.Collections.Generic;
using System.Linq;

using Saab.Utility.GfxCaps;
using Saab.Foundation.Unity.MapStreamer.Utils;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Resources
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Runtime;

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

}
