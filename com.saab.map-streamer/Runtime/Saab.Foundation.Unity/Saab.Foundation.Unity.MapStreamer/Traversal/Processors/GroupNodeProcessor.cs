// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class GroupNodeProcessor : NodeProcessor<Group>
    {
        public GroupNodeProcessor(SceneManager sceneManager) : base(sceneManager) { }

        protected override TraversalResult Process(
            Group node,
            ref TraversalContext context)
        {
            SceneManager.TraverseChildren(node, in context);
            return TraversalResult.Created(context.NodeHandle.gameObject);
        }
    }
}
