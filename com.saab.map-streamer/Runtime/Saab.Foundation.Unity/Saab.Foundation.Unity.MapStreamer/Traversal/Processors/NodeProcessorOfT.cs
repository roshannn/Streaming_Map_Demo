// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal abstract class NodeProcessor<TNode> : NodeProcessor
        where TNode : Node
    {
        protected NodeProcessor(SceneManager sceneManager)
        {
            SceneManager = sceneManager;
        }

        protected SceneManager SceneManager { get; }

        public sealed override TraversalResult Process(
            Node node,
            ref TraversalContext context)
        {
            return Process((TNode)node, ref context);
        }

        protected abstract TraversalResult Process(
            TNode node,
            ref TraversalContext context);
    }
}
