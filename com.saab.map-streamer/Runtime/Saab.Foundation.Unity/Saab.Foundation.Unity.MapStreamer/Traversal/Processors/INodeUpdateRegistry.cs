// Copyright 2021 saab AB

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal interface INodeUpdateRegistry
    {
        void RegisterForUpdate(NodeHandle nodeHandle);
        void Unregister(NodeHandle nodeHandle);
        void UpdateNodes();
        void Clear();
    }
}
