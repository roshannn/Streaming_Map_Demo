// Copyright 2021 saab AB

using System;
using System.Collections.Generic;
using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class NodeProcessorFactory
    {
        private readonly Dictionary<Type, NodeProcessor> _processors =
            new Dictionary<Type, NodeProcessor>();

        public NodeProcessorFactory(
            NodeProcessorComposer composer,
            NodeEvents nodeEvents)
        {
            Register<RefNode>(composer.Compose(new RefNodeProcessor(nodeEvents)));
            Register<Geometry>(composer.Compose(new GeometryNodeProcessor(nodeEvents)));
            Register<Crossboard>(composer.Compose(new CrossboardNodeProcessor(nodeEvents)));
            Register<Roi>(composer.Compose(new RoiProcessor(nodeEvents)));
            Register<RoiNode>(composer.Compose(new RoiNodeProcessor(nodeEvents)));
            Register<Transform>(composer.Compose(new TransformNodeProcessor(nodeEvents)));
            Register<DynamicLoader>(composer.Compose(new DynamicLoaderNodeProcessor(nodeEvents)));
            Register<Lod>(composer.Compose(new LodNodeProcessor(nodeEvents)));
            Register<Group>(composer.Compose(new GroupNodeProcessor(nodeEvents)));
            Register<ExtRef>(composer.Compose(new ExternalReferenceNodeProcessor(nodeEvents)));
        }

        public NodeProcessor Resolve(Node node)
        {
            var nodeType = node.GetType();

            while (nodeType != null)
            {
                if (_processors.TryGetValue(nodeType, out var processor))
                    return processor;

                nodeType = nodeType.BaseType;
            }

            return null;
        }

        private void Register<TNode>(NodeProcessor<TNode> processor)
            where TNode : Node
        {
            _processors.Add(typeof(TNode), processor);
        }
    }
}
