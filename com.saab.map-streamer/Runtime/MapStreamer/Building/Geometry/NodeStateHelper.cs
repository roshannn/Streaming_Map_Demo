//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s) 
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied. 
//
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: NodeStateHelper.cs
// Module		:
// Description	: Helper for texture and state uploads
// Author		: Anders Modén
// Product		: Gizmo3D 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...
//
// Who	Date	Description
//
// ZJP	200625	Created file                                        (2.10.6)
//
//******************************************************************************

// Framework
using System;
using System.Collections.Generic;

// Unity
using UnityEngine;

// GizmoSDK
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using gzTexture = GizmoSDK.Gizmo3D.Texture;
using Texture = UnityEngine.Texture;
namespace Saab.Foundation.Unity.MapStreamer
{
    public struct StateBuildOutput
    {
        public Texture2D Texture;
        public Texture2D Feature;
        public Texture2D SurfaceHeight;

        public Matrix3D Feature_homography;
        public Matrix3D SurfaceHeight_homography;
    }

    public class TextureImageInfo
    {
        public DynamicType homography;

        public DynamicType border_x;
        public DynamicType border_y;
    }

    public static class StateHelper
    {
        private enum MissingTextureBehavior
        {
            Fail,
            UseWhitePlaceholder
        }

        private static readonly Dictionary<TextureFormat, bool> _supportedFormats = new Dictionary<TextureFormat, bool>();

        public static bool Build(State state, out StateBuildOutput output, TextureManager textureCache = null)
        {
            output = default;

            if (!ReadTextureFromState(
                state,
                out Texture2D texture,
                textureCache,
                out _,
                MissingTextureBehavior.UseWhitePlaceholder,
                false))
                return false;

            output.Texture = texture;

            if (ReadTextureFromState(
                state,
                out Texture2D feature,
                textureCache,
                out TextureImageInfo info,
                MissingTextureBehavior.Fail,
                true,
                1,
                false))       // Features always singletons and no mipmap force
            {
                if (info != null && info.homography.Is("gzImageHomography"))
                {
                    output.Feature = feature;
                    output.Feature_homography = ((ImageHomography)info.homography);
                }
                else
                    output.Feature = null;
            }

            if (ReadTextureFromState(
                state,
                out Texture2D surface,
                textureCache,
                out info,
                MissingTextureBehavior.Fail,
                true,
                2,
                false))       // Features always singletons and no mipmap force
            {
                if (info != null && info.homography.Is("gzImageHomography"))
                {
                    output.SurfaceHeight = surface;
                    output.SurfaceHeight_homography = ((ImageHomography)info.homography);
                }
                else
                    output.SurfaceHeight = null;
            }

            return true;
        }

        public static bool Build(State state, out Texture2D output, TextureManager textureCache = null)
        {
            output = default;

            if (!ReadTextureFromState(
                state,
                out Texture2D texture,
                textureCache,
                out _,
                MissingTextureBehavior.UseWhitePlaceholder,
                false))
                return false;

            output = texture;

            return true;
        }

        private static bool ReadTextureFromState(
            State state,
            out Texture2D result,
            TextureManager textureCache,
            out TextureImageInfo info,
            MissingTextureBehavior missingTextureBehavior,
            bool readMetadata,
            uint unit = 0,
            bool useMipMaps = true)
        {
            result = null;
            info = null;

            if (!state.HasTexture(unit) || state.GetMode(StateMode.TEXTURE) != StateModeActivation.ON)
            {
                if (missingTextureBehavior == MissingTextureBehavior.Fail)
                    return false;

                if (textureCache != null)
                {
                    var ptr = state.GetNativeReference();

                    if (textureCache.TryGet(ptr, out Texture cachedTexture, out _))
                    {
                        result = (Texture2D)cachedTexture;
                        return true;
                    }

                    result = CopyWhiteTexture();
                    return textureCache.TryAdd(ptr, result, null);
                }

                result = CopyWhiteTexture();
                return true;
            }

            using (var texture = state.GetTexture(unit))
            {
                try
                {
                    if (textureCache != null)
                    {
                        var ptr = texture.GetNativeReference();

                        if (textureCache.TryGet(ptr, out Texture cachedTexture, out TextureImageInfo cachedInfo))
                        {
                            result = (Texture2D)cachedTexture;
                            info = readMetadata ? cachedInfo : null;
                            return true;
                        }

                        info = readMetadata ? new TextureImageInfo() : null;

                        if (!CopyTexture(texture, out result, info, useMipMaps))
                            return false;

                        return textureCache.TryAdd(ptr, result, info);
                    }

                    info = readMetadata ? new TextureImageInfo() : null;

                    if (!CopyTexture(texture, out result, info, useMipMaps))
                        return false;
                }
                finally
                {
                    // prevent dispose from locking object by releasing it manually here
                    texture.ReleaseAlreadyLocked();
                }
            }

            return true;
        }

        private static Texture2D CopyWhiteTexture()
        {
            Texture2D src = Texture2D.whiteTexture;
            Texture2D dst = new Texture2D(src.width, src.height, src.format, src.mipmapCount > 1);
            Graphics.CopyTexture(src, dst);
            return dst;
        }

        private static bool CopyTexture(
            gzTexture gzTexture,
            out Texture2D result,
            TextureImageInfo info,
            bool useMipMaps = true)
        {
            result = null;

            if (!gzTexture.HasImage())
                return false;

            using (var image = gzTexture.GetImage())
            {
                if (!TryGetImageData(
                    gzTexture,
                    image,
                    useMipMaps,
                    out IntPtr nativePtr,
                    out uint size,
                    out uint width,
                    out uint height,
                    out TextureFormat textureFormat))
                    return false;

                result = AcquireUnityTexture(
                    gzTexture,
                    width,
                    height,
                    textureFormat,
                    useMipMaps,
                    out bool canBeRecycled);
                if (!result)
                    return false;

                UploadTextureData(
                    result,
                    gzTexture.MinFilter,
                    nativePtr,
                    size,
                    useMipMaps,
                    canBeRecycled);
                ReadImageMetadata(image, info);
            }

            return true;
        }

        private static bool TryGetImageData(
            gzTexture gzTexture,
            Image image,
            bool useMipMaps,
            out IntPtr nativePtr,
            out uint size,
            out uint width,
            out uint height,
            out TextureFormat textureFormat)
        {
            ImageFormat imageFormat = image.Format;
            ComponentType componentType = image.ComponentType;
            textureFormat = GizmoTextureFormatConverter.ToUnityFormat(imageFormat, componentType);
            bool uncompress = !IsTextureFormatSupported(textureFormat);
            nativePtr = IntPtr.Zero;

            if (!gzTexture.GetMipMapImageArray(
                ref nativePtr,
                out size,
                out ImageFormat finalImageFormat,
                out ComponentType finalComponentType,
                out _,
                out width,
                out height,
                out uint depth,
                useMipMaps,
                uncompress))
                return false;

            if (depth != 1)
                return false;

            // With uncompress enabled, the image may have been mutated.
            if (finalImageFormat != imageFormat || finalComponentType != image.ComponentType)
            {
                textureFormat = GizmoTextureFormatConverter.ToUnityFormat(
                    finalImageFormat,
                    finalComponentType);

                if (!IsTextureFormatSupported(textureFormat))
                    return false;
            }

            return true;
        }

        private static Texture2D AcquireUnityTexture(
            gzTexture gzTexture,
            uint width,
            uint height,
            TextureFormat textureFormat,
            bool useMipMaps,
            out bool canBeRecycled)
        {
            // Reusing texture objects when streaming terrain almost doubles performance.
            var texture = Texture2DCache.GetOrCreateTexture(
                (int)width,
                (int)height,
                textureFormat,
                useMipMaps,
                out canBeRecycled);

            texture.wrapModeU = GizmoTextureFormatConverter.ToUnityWrapMode(gzTexture.WrapS);
            texture.wrapModeV = GizmoTextureFormatConverter.ToUnityWrapMode(gzTexture.WrapT);
            texture.wrapModeW = GizmoTextureFormatConverter.ToUnityWrapMode(gzTexture.WrapR);

#if DEBUG
            texture.name = "SM - NodeTexture";
#endif

            return texture;
        }

        private static void UploadTextureData(
            Texture2D texture,
            gzTexture.TextureMinFilter minFilter,
            IntPtr nativePtr,
            uint size,
            bool useMipMaps,
            bool canBeRecycled)
        {
            texture.LoadRawTextureData(nativePtr, (int)size);
            texture.filterMode = GizmoTextureFormatConverter.ToUnityFilterMode(minFilter);

            // Recycled textures retain their CPU-side buffer so they can be updated.
            texture.Apply(useMipMaps, makeNoLongerReadable: !canBeRecycled);
        }

        private static void ReadImageMetadata(Image image, TextureImageInfo info)
        {
            if (info == null)
                return;

            info.homography = image.GetAttribute("UserDataImInfo", "ImI-Wrld-Hom");
            info.border_x = image.GetAttribute("UserDataImInfo", "ImI-Pixel-X-border");
            info.border_y = image.GetAttribute("UserDataImInfo", "ImI-Pixel-Y-border");
        }

        private static bool IsTextureFormatSupported(TextureFormat format)
        {
            if (!_supportedFormats.TryGetValue(format, out bool supported))
            {
                // SystemInfo.SupportsTextureFormat is a very slow operation, so
                // we will cache the result for future queries.
                supported = SystemInfo.SupportsTextureFormat(format);
                _supportedFormats.Add(format, supported);

                if (!supported)
                    Message.Send("StateBuilder", MessageLevel.WARNING, $"{format} was not a supported format!");
            }

            return supported;
        }
    }
}
