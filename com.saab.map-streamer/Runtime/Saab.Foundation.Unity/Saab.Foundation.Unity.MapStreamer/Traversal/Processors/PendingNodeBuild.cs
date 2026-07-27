// Copyright 2021 saab AB

using Saab.Foundation.Unity.MapStreamer.NodeProcessing;

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
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
