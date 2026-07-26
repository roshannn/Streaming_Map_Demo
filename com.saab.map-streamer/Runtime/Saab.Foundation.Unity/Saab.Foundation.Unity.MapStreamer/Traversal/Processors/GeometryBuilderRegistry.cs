// Copyright 2021 saab AB

using System.Collections;
using System.Collections.Generic;
using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class GeometryBuilderRegistry : IEnumerable<INodeBuilder>
    {
        private readonly List<INodeBuilder> _builders = new List<INodeBuilder>();

        public int Count => _builders.Count;

        public void Add(INodeBuilder builder) => _builders.Add(builder);
        public void Remove(INodeBuilder builder) => _builders.Remove(builder);

        public INodeBuilder Resolve(Node node, in TraversalContext context)
        {
            for (var i = 0; i < _builders.Count; ++i)
            {
                var builder = _builders[i];
                if (builder.CanBuild(
                    node,
                    context.TraversalStateFlags,
                    context.IntersectMask))
                    return builder;
            }

            return null;
        }

        public void Reset()
        {
            foreach (var builder in _builders)
                builder.Reset();
        }

        public IEnumerator<INodeBuilder> GetEnumerator() => _builders.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
