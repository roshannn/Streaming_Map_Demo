using Saab.Foundation.Unity.MapStreamer.Building.Coordination;
using Saab.Foundation.Unity.MapStreamer.Nodes.Pooling;
using Saab.Foundation.Unity.MapStreamer.Nodes.Processing;
using Saab.Foundation.Unity.MapStreamer.Streaming;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer.Composition.Building
{
    internal sealed class BuildingInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GeometryBuilderRegistry>(Lifetime.Scoped);
            builder.Register<PooledNodeObjectPolicyRegistry>(Lifetime.Scoped);
            builder.Register<NodeBuildCoordinator>(Lifetime.Scoped)
                .AsSelf()
                .As<IBuildScheduler>();
            builder.Register<TextureManager>(Lifetime.Scoped);
            builder.Register<MaterialManager>(Lifetime.Scoped);
            builder.Register<NodeHandlePool>(Lifetime.Scoped)
                .AsSelf()
                .As<INodePoolMaintenance>();
            builder.Register<NodeHierarchyUnloader>(Lifetime.Scoped);
        }
    }
}
