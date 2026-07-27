// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal interface IReferenceNodeOperations
    {
        TraversalNode Process(RefNode node, in TraversalContext context);
    }
}
