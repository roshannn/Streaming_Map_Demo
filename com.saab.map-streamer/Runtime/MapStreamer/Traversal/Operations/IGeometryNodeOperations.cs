// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Operations
{
    internal interface IGeometryNodeOperations
    {
        TraversalNode Process(Geometry node, in TraversalContext context);
        void Reset();
    }
}
