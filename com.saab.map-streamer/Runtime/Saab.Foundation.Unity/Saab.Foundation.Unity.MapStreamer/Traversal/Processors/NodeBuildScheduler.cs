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
                    context.Node.Build(
                        builder,
                        context.ActiveStateNode);
                    break;
                case BuildPriority.Low:
                    _pending.Enqueue(new PendingNodeBuild(
                        builder,
                        context.Node,
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
                var node = build.Node;

                if (build.Version != node.Version)
                    continue;

                var activeStateNode = build.ActiveStateNode;
                if (activeStateNode.IsValid &&
                    !activeStateNode.HasNativeNode)
                    continue;

                node.Build(build.Builder, activeStateNode);
            }
        }

        public void Clear() => _pending.Clear();
    }
}
