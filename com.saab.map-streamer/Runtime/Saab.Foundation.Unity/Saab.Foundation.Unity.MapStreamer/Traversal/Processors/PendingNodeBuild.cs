// Copyright 2021 saab AB

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
            Node = node;
            ActiveStateNode = activeStateNode;
            Version = node.Version;
        }

        public INodeBuilder Builder { get; }
        public TraversalNode Node { get; }
        public TraversalNode ActiveStateNode { get; }
        public byte Version { get; }
    }
}
