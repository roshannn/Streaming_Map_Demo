// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal interface INodeHandleFactory
    {
        NodeHandle Create(Node node, PoolObjectFeature feature);
    }
}
