// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal interface IGeometryNodeOperations
    {
        GameObject Process(Geometry node, in TraversalContext context);
    }
}
