// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class LodNodeProcessor : NodeProcessor<Lod>
    {
        public LodNodeProcessor(SceneManager sceneManager) : base(sceneManager) { }

        protected override TraversalResult Process(
            Lod node,
            ref TraversalContext context)
        {
            SceneManager.TraverseChildren(node, in context, true);
            SceneManager.NotifyNewLod(
                context.NodeHandle.gameObject,
                context.TraversalStateFlags.HasFlag(TraversalState.Asset));

            return TraversalResult.Created(context.NodeHandle.gameObject);
        }
    }
}
