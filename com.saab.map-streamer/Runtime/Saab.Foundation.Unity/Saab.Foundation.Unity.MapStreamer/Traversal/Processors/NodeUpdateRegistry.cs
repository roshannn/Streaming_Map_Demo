// Copyright 2021 saab AB

using System.Collections.Generic;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class NodeUpdateRegistry : INodeUpdateRegistry
    {
        private readonly LinkedList<GameObject> _nodes =
            new LinkedList<GameObject>();

        public void RegisterForUpdate(TraversalNode node)
        {
            node.EnableTransformUpdates();
            _nodes.AddLast(node.GameObject);
        }

        public void Unregister(NodeHandle nodeHandle)
        {
            if (!nodeHandle.inNodeUpdateList)
                return;

            _nodes.Remove(nodeHandle.gameObject);
            nodeHandle.inNodeUpdateList = false;
        }

        public void UpdateNodes()
        {
            foreach (var gameObject in _nodes)
                gameObject.GetComponent<NodeHandle>().UpdateNodeInternals();
        }

        public void Clear()
        {
            _nodes.Clear();
        }
    }
}
