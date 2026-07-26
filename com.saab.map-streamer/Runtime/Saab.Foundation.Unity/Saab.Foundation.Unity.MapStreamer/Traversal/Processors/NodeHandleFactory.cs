// Copyright 2021 saab AB

using System;
using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class NodeHandleFactory : INodeHandleFactory
    {
        private readonly Func<PoolObjectFeature, Node, NodeHandle> _allocate;

        public NodeHandleFactory(Func<PoolObjectFeature, Node, NodeHandle> allocate)
        {
            _allocate = allocate;
        }

        public NodeHandle Create(Node node, PoolObjectFeature feature)
        {
            var nodeHandle = _allocate(feature, node);

#if UNITY_EDITOR
            nodeHandle.name = node.Name;
            if (string.IsNullOrEmpty(nodeHandle.name))
                nodeHandle.name = node.GetType().Name;
#endif

            return nodeHandle;
        }
    }
}
