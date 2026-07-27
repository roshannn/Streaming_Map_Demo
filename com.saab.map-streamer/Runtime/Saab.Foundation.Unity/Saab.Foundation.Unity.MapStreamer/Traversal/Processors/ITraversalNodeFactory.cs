// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal interface ITraversalNodeFactory
    {
        TraversalNode Create(Node node, PoolObjectFeature feature);
    }
}
