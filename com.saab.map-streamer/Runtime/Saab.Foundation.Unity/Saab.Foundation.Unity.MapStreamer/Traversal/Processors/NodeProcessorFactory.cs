// Copyright 2021 saab AB

using System;
using System.Collections.Generic;
using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class NodeProcessorFactory
    {
        private readonly Dictionary<Type, NodeProcessor> _processors =
            new Dictionary<Type, NodeProcessor>();

        public NodeProcessorFactory(SceneManager sceneManager)
        {
            Register<RefNode>(new RefNodeProcessor(sceneManager));
            Register<Geometry>(new GeometryNodeProcessor(sceneManager));
            Register<Crossboard>(new CrossboardNodeProcessor(sceneManager));
            Register<Roi>(new RoiProcessor(sceneManager));
            Register<RoiNode>(new RoiNodeProcessor(sceneManager));
            Register<Transform>(new TransformNodeProcessor(sceneManager));
            Register<DynamicLoader>(new DynamicLoaderNodeProcessor(sceneManager));
            Register<Lod>(new LodNodeProcessor(sceneManager));
            Register<Group>(new GroupNodeProcessor(sceneManager));
            Register<ExtRef>(new ExternalReferenceNodeProcessor(sceneManager));
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
