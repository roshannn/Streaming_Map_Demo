// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal abstract class NodeProcessor
    {
        public virtual bool RequiresDefaultTraversalNode => true;

        public abstract TraversalResult Process(
            Node node,
            ref TraversalContext context);
    }
}
