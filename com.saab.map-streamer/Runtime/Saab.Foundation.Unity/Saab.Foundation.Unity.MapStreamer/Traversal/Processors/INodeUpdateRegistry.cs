// Copyright 2021 saab AB

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal interface INodeUpdateRegistry
    {
        void RegisterForUpdate(TraversalNode node);
        void Unregister(NodeHandle nodeHandle);
        void UpdateNodes();
        void Clear();
    }
}
