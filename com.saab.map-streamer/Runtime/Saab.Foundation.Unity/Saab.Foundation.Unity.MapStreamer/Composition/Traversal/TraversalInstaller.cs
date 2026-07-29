using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Nodes.Processing;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Contracts;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Foundation.Unity.MapStreamer.Traversal.Operations;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer.Composition.Traversal
{
    internal sealed class TraversalInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<NodeEvents>();
            builder.Register<TraversalConfiguration>(Lifetime.Scoped)
                .As<ITraversalConfiguration>();
            builder.Register<NodeUpdateRegistry>(Lifetime.Scoped)
                .As<INodeUpdateRegistry>()
                .As<INodeUpdateProcessor>();
            builder.Register<TraversalNodeFactory>(Lifetime.Scoped)
                .As<ITraversalNodeFactory>();
            builder.Register<GeometryNodeOperations>(Lifetime.Scoped)
                .As<IGeometryNodeOperations>();
            builder.Register<SceneTraverser>(Lifetime.Scoped);
        }
    }
}
