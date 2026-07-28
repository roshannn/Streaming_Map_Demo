// Copyright 2021 saab AB

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Contracts
{
    internal interface INodeUpdateRegistry
    {
        void RegisterForUpdate(TraversalNode node);
        void Unregister(NodeHandle nodeHandle);
        void UpdateNodes();
        void Clear();
    }
}
