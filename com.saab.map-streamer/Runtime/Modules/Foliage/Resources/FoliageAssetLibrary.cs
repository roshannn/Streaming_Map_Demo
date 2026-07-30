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

namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

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

            var resolution = Mathf.NextPowerOfTwo(Mathf.Max(
                foliage.Max(value => value.MainTexture.width),
                foliage.Max(value => value.MainTexture.height)));
            var result = new Texture2DArray(
                resolution,
                resolution,
                foliage.Count,
                format,
                true)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            var stagingTarget = new RenderTexture(
                resolution,
                resolution,
                0,
                RenderTextureFormat.ARGB32)
            {
                useMipMap = true,
                name = "Foliage Asset Library Staging"
            };
            var previous = RenderTexture.active;
            try
            {
                for (var index = 0; index < foliage.Count; ++index)
                {
                    Graphics.Blit(foliage[index].MainTexture, stagingTarget);
                    RenderTexture.active = stagingTarget;
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
                        Graphics.CopyTexture(staging, 0, result, index);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(staging);
                    }
                }
                result.Apply(false, true);
                return result;
            }
            catch
            {
                UnityEngine.Object.Destroy(result);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                stagingTarget.Release();
                UnityEngine.Object.DestroyImmediate(stagingTarget);
            }
        }
    }

}
