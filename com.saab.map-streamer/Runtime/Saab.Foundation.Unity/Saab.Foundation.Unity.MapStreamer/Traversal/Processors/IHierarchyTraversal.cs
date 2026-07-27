// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal interface IHierarchyTraversal
    {
        void TraverseChildren(
            Group group,
            in TraversalContext context,
            bool addActionInterfaces = false);
    }
}
