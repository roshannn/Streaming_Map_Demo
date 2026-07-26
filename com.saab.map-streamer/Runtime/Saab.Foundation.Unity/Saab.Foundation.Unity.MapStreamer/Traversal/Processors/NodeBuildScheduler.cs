// Copyright 2021 saab AB

using System;
using System.Collections.Generic;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal sealed class NodeBuildScheduler
    {
        private readonly Queue<PendingNodeBuild> _pending =
            new Queue<PendingNodeBuild>(1000);

        public void Build(INodeBuilder builder, in TraversalContext context)
        {
            switch (builder.Priority)
            {
                case BuildPriority.Immediate:
                    if (builder.Build(context.NodeHandle, context.ActiveStateNode))
                        context.NodeHandle.builder = builder;
                    break;
                case BuildPriority.Low:
                    _pending.Enqueue(new PendingNodeBuild(
                        builder,
                        context.NodeHandle,
                        context.ActiveStateNode));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public void Process(TimeSpan maxBuildTime)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            while (_pending.Count > 0 && timer.Elapsed < maxBuildTime)
            {
                var build = _pending.Dequeue();
                var nodeHandle = build.NodeHandle;

                if (build.Version != nodeHandle.version)
                    continue;

                var activeStateNode = build.ActiveStateNode;
                if (activeStateNode != null && activeStateNode.node == null)
                    continue;

                if (build.Builder.Build(nodeHandle, activeStateNode))
                    nodeHandle.builder = build.Builder;
            }
        }

        public void Clear() => _pending.Clear();
    }
}
