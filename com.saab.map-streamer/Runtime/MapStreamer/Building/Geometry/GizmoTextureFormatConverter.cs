using System;

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using UnityEngine;

using gzTexture = GizmoSDK.Gizmo3D.Texture;

namespace Saab.Foundation.Unity.MapStreamer
{
    internal static class GizmoTextureFormatConverter
    {
        internal static TextureFormat ToUnityFormat(ImageFormat imageFormat, ComponentType componentType)
        {
            switch (imageFormat)
            {
                case ImageFormat.RGBA:
                    if (componentType == ComponentType.UNSIGNED_BYTE)
                        return TextureFormat.RGBA32;
                    if (componentType == ComponentType.FLOAT)
                        return TextureFormat.RGBA32;
                    if (componentType == ComponentType.HALF_FLOAT)
                        return TextureFormat.RGBAHalf;
                    break;

                case ImageFormat.RGB:
                    if (componentType == ComponentType.UNSIGNED_BYTE)
                        return TextureFormat.RGB24;
                    break;

                case ImageFormat.COMPRESSED_RGBA8_ETC2:
                    return TextureFormat.ETC2_RGBA8;

                case ImageFormat.COMPRESSED_RGB8_ETC2:
                    return TextureFormat.ETC2_RGB;

                case ImageFormat.COMPRESSED_RGBA_S3TC_DXT1:
                case ImageFormat.COMPRESSED_RGB_S3TC_DXT1:
                    return TextureFormat.DXT1;

                case ImageFormat.COMPRESSED_RGBA_S3TC_DXT5:
                    return TextureFormat.DXT5;

                case ImageFormat.LUMINANCE:
                    if (componentType == ComponentType.UNSIGNED_BYTE)
                        return TextureFormat.R8;
                    if (componentType == ComponentType.FLOAT)
                        return TextureFormat.RFloat;
                    break;

                case ImageFormat.LUMINANCE_ALPHA:
                    if (componentType == ComponentType.UNSIGNED_BYTE)
                        return TextureFormat.RG16;
                    if (componentType == ComponentType.FLOAT)
                        return TextureFormat.RGFloat;
                    break;
            }

            throw new NotSupportedException();
        }

        internal static TextureWrapMode ToUnityWrapMode(gzTexture.TextureWrapMode wrapMode)
        {
            switch (wrapMode)
            {
                case gzTexture.TextureWrapMode.CLAMP_TO_EDGE:
                case gzTexture.TextureWrapMode.CLAMP_TO_BORDER:
                case gzTexture.TextureWrapMode.CLAMP:
                    return TextureWrapMode.Clamp;
                case gzTexture.TextureWrapMode.REPEAT:
                    return TextureWrapMode.Repeat;
                case gzTexture.TextureWrapMode.MIRRORED_REPEAT:
                    return TextureWrapMode.Mirror;
                default:
                    throw new NotSupportedException();
            }
        }

        internal static FilterMode ToUnityFilterMode(gzTexture.TextureMinFilter minFilter)
        {
            switch (minFilter)
            {
                case gzTexture.TextureMinFilter.LINEAR:
                case gzTexture.TextureMinFilter.LINEAR_MIPMAP_NEAREST:
                    return FilterMode.Bilinear;
                case gzTexture.TextureMinFilter.LINEAR_MIPMAP_LINEAR:
                    return FilterMode.Trilinear;
                default:
                    return FilterMode.Point;
            }
        }
    }
}
