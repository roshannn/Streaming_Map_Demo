// Copyright 2021 saab AB

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal readonly struct PendingNodeBuild
    {
        public PendingNodeBuild(
            INodeBuilder builder,
            NodeHandle nodeHandle,
            NodeHandle activeStateNode)
        {
            Builder = builder;
            NodeHandle = nodeHandle;
            ActiveStateNode = activeStateNode;
            Version = nodeHandle.version;
        }

        public INodeBuilder Builder { get; }
        public NodeHandle NodeHandle { get; }
        public NodeHandle ActiveStateNode { get; }
        public byte Version { get; }
    }
}
