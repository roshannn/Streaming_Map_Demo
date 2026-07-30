using System;
using System.Collections.Generic;
using System.Linq;

using Saab.Utility.GfxCaps;
using Saab.Foundation.Unity.MapStreamer.Utils;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Rendering
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Runtime;

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

}
