// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class TraversalNodeFactory : ITraversalNodeFactory
    {
        private readonly NodeHandlePool _pool;

        public TraversalNodeFactory(NodeHandlePool pool)
        {
            _pool = pool;
        }

        public TraversalNode Create(Node node, PoolObjectFeature feature)
        {
            var nodeHandle = _pool.Allocate(feature, node);

#if UNITY_EDITOR
            nodeHandle.name = node.Name;
            if (string.IsNullOrEmpty(nodeHandle.name))
                nodeHandle.name = node.GetType().Name;
#endif

            return new TraversalNode(nodeHandle);
        }
    }
}
