/* 
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s) 
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been
 * supplied. 
 * 
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 */

using UnityEngine;
using System.Collections.Generic;
using System;

using System.Runtime.InteropServices;
using Saab.Foundation.Map;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    public partial class FoliageFeature : IDisposable
    {
        // list of all instances currently being rendered
        private readonly List<FeatureData> _items = new List<FeatureData>(128);
        // if a go exists in the render list, it exists in this lookup, used to avoid searching the list
        private readonly HashSet<TerrainModuleIdentity> _itemLookup =
            new HashSet<TerrainModuleIdentity>();

        private Vector2 _resolution;
        private readonly ComputeShader _placement;
        private readonly int _kernelCull;
        private readonly int _kernelClear;
        private readonly int _kernelPlacement;
        private readonly float _density;
        private readonly float _scale = 10000;

        // *********** buffers ***********
        private ComputeBuffer _mappingBuffer;
        private Vector2 _fov;
        private readonly ComputeBuffer _pointCloud;

        private readonly ComputeBuffer _pointCloudCulled;
        private readonly ComputeBuffer _angleDepth;
        private readonly int _KernelPreCull;
        private readonly int _kernelPostCull;
        private const float _depthBufferScale = 2.5f;
        private readonly int _foliageStride;
        private readonly IMapCoordinates _mapCoordinates;

        public int FoliageCount
        {
            get { return _items.Count; }
        }

        public FoliageFeature(
            int BufferSize,
            float density,
            int[] map,
            ComputeShader computeShader,
            IMapCoordinates mapCoordinates)
        {
            _mapCoordinates = mapCoordinates;
            _foliageStride = Marshal.SizeOf<FoliagePoint>();

            _placement = computeShader;
            _kernelCull = _placement.FindKernel("CSCull");
            _kernelClear = _placement.FindKernel("CSClear");
            _kernelPlacement = _placement.FindKernel("CSPlacement");
            _kernelPostCull = _placement.FindKernel("CSPostCull");
            _KernelPreCull = _placement.FindKernel("CSPreCull");


            _placement.SetFloat(PlacementParameterID.AngleResolutionScale, _depthBufferScale);
            _angleDepth = new ComputeBuffer(Mathf.CeilToInt(180 * _depthBufferScale * 180 * _depthBufferScale), sizeof(uint));

            _density = density;
            _pointCloud = new ComputeBuffer(BufferSize <= 0 ? 1 : BufferSize, _foliageStride, ComputeBufferType.Append);
            _pointCloudCulled = new ComputeBuffer(BufferSize <= 0 ? 1 : BufferSize, _foliageStride, ComputeBufferType.Append);
            _mappingBuffer = new ComputeBuffer(map.Length, sizeof(int));
            _mappingBuffer.SetData(map);
        }

        public bool AddFoliage(
            TerrainModuleIdentity identity,
            GameObject go,
            NodeHandle node,
            ComputeBuffer pixelToObject,
            Texture surfaceHeight = null)
        {
            if (_itemLookup.Contains(identity))
                return false;
            if (pixelToObject == null)
                return false;

            Texture2D featureMap = node.feature;
            if (featureMap == null)
                return false;

            Texture height = node.surfaceHeight ?? surfaceHeight;
            if (height == null)
                return false;

            _resolution = new Vector2((float)node.featureInfo.v11, (float)node.featureInfo.v22);
            var size = FindBufferSize(featureMap);

            if (size >= ushort.MaxValue * 128)
                return false;

            var maxside = Mathf.Max(featureMap.width, featureMap.height);

            Texture2D texture = node.texture;
            var data = new FeatureData(
                identity,
                go,
                node.featureInfo,
                _density,
                (uint)maxside,
                _scale)
            {
                FeatureMap = featureMap,
                Texture = texture,
                surfaceHeight = height,
                PixelToObject = pixelToObject
            };

            data.TerrainPoints = new ComputeBuffer(size < 1 ? 1 : size, _foliageStride, ComputeBufferType.Append);

            FeaturePlacement(data);
            // Placement consumes these synchronously. The module owns the
            // shared inputs and releases them once after all feature sets.
            data.PixelToObject = null;
            data.surfaceHeight = null;

            _items.Add(data);
            _itemLookup.Add(identity);

            return true;
        }
        public void RemoveFoliage(TerrainModuleIdentity identity)
        {
            if (!_itemLookup.Remove(identity))
                return;

            for (var i = 0; i < _items.Count; ++i)
            {
                if (!_items[i].Identity.Equals(identity))
                    continue;

                ClearFeature(_items[i]);

                if ((i + 1) < _items.Count)
                    _items[i] = _items[_items.Count - 1];

                _items.RemoveAt(_items.Count - 1);

                return;
            }
        }
        public void Dispose()
        {
            _pointCloud?.Release();
            _pointCloudCulled?.Release();
            _mappingBuffer?.Release();
            _angleDepth?.Release();

            for (var i = 0; i < _items.Count; ++i)
            {
                ClearFeature(_items[i]);
            }
            _items.Clear();
            _itemLookup.Clear();
        }

        // needed to clear old valid tree data from gpu memory, if skipped when frustum culling old trees might get valid/visable
        private void ClearFeature(in FeatureData data)
        {
            data.TerrainPoints.SetCounterValue(0);

            _placement.SetBuffer(_kernelClear, PlacementParameterID.TerrainPoints, data.TerrainPoints);
            _placement.SetInt(PlacementParameterID.BufferCount, data.TerrainPoints.count);
            if (data.TerrainPoints.count > 0)
                _placement.Dispatch(_kernelClear, Mathf.CeilToInt(data.TerrainPoints.count / 128f), 1, 1);

            data.Dispose();
        }

        private int FindBufferSize(Texture2D featureMap)
        {
            var maxSize =
                Mathf.CeilToInt(featureMap.width * _resolution.x * _density) *
                Mathf.CeilToInt(featureMap.height * _resolution.y * _density);

            return Mathf.CeilToInt(maxSize) < 1 ? 1 : Mathf.CeilToInt(maxSize);
        }

        private void FeaturePlacement(FeatureData node)
        {
            _placement.SetTexture(_kernelPlacement, PlacementParameterID.SplatMap, node.FeatureMap);
            _placement.SetTexture(_kernelPlacement, PlacementParameterID.Texture, node.Texture);
            _placement.SetBuffer(_kernelPlacement, PlacementParameterID.PixelToObjectCoord, node.PixelToObject);

            _placement.SetTexture(_kernelPlacement, PlacementParameterID.HeightSurface, node.surfaceHeight);

            _placement.SetVector(PlacementParameterID.heightResolution, new Vector2(node.surfaceHeight.width, node.surfaceHeight.height));
            _placement.SetBuffer(_kernelPlacement, PlacementParameterID.TerrainPoints, node.TerrainPoints);
            _placement.SetBuffer(_kernelPlacement, PlacementParameterID.PixelToWorld, node.PlacementMatrix);

            // we need to set this everytime
            _placement.SetBuffer(_kernelPlacement, PlacementParameterID.FeatureMap, _mappingBuffer);

            int threadsX = Mathf.CeilToInt(node.FeatureMap.width / 4f);
            int threadsY = Mathf.CeilToInt(node.FeatureMap.height / 4f);

            _placement.SetMatrix(PlacementParameterID.ObjToWorld, LocalToWorldMatrix(node.Object));

            node.TerrainPoints.SetCounterValue(0);
            _placement.Dispatch(_kernelPlacement, threadsX < 1 ? 1 : threadsX, threadsY < 1 ? 1 : threadsY, 1);
        }
    }
}
