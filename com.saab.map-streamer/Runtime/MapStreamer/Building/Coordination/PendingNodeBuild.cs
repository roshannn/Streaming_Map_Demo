// Copyright 2021 saab AB

using Saab.Foundation.Unity.MapStreamer.Nodes.Pooling;
using Saab.Foundation.Unity.MapStreamer.Nodes.Processing;
using Saab.Foundation.Unity.MapStreamer.Traversal;

namespace Saab.Foundation.Unity.MapStreamer.Building.Coordination
{
    internal readonly struct PendingNodeBuild
    {
        public PendingNodeBuild(
            INodeBuilder builder,
            TraversalNode node,
            TraversalNode activeStateNode)
        {
            Builder = builder;
            Target = node.BuildTarget;
            ActiveStateTarget = activeStateNode.IsValid
                ? activeStateNode.BuildTarget
                : default;
            Version = Target.Version;
        }

        public INodeBuilder Builder { get; }
        public NodeBuildTarget Target { get; }
        public NodeBuildTarget ActiveStateTarget { get; }
        public byte Version { get; }
    }
}
