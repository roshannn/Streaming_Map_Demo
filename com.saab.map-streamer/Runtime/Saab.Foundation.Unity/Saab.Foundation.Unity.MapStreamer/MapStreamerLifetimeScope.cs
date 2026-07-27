using System;

using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Streaming.Pipeline;
using Saab.Foundation.Unity.MapStreamer.Streaming.Synchronization;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;
using Saab.Unity.Initializer;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer
{
    public sealed class MapStreamerLifetimeScope : LifetimeScope
    {
        [UnityEngine.SerializeField]
        private MapConfig mapConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            if (mapConfig == null)
                throw new InvalidOperationException(
                    "MapConfig must be assigned on MapStreamerLifetimeScope.");

            builder.RegisterInstance(mapConfig);

            builder.RegisterComponentInHierarchy<NodeEvents>();
            builder.RegisterComponentInHierarchy<SceneManager>();
            builder.RegisterComponentInHierarchy<CameraControl>();
            builder.RegisterComponentInHierarchy<Initializer>();
            builder.RegisterComponentInHierarchy<MapShadingModule>();
            builder.RegisterComponentInHierarchy<FoliageModule>();

            builder.Register<TraversalConfiguration>(Lifetime.Scoped)
                .As<ITraversalConfiguration>();
            builder.Register<NodeUpdateRegistry>(Lifetime.Scoped)
                .As<INodeUpdateRegistry>();
            builder.Register<ExternalAssetLoader>(Lifetime.Scoped)
                .As<IExternalAssetQueue>();
            builder.Register<TraversalNodeFactory>(Lifetime.Scoped)
                .As<ITraversalNodeFactory>();
            builder.Register<GeometryNodeOperations>(Lifetime.Scoped)
                .As<IGeometryNodeOperations>();

            builder.Register<GeometryBuilderRegistry>(Lifetime.Scoped);
            builder.Register<PooledNodeObjectPolicyRegistry>(Lifetime.Scoped);
            builder.Register<NodeBuildCoordinator>(Lifetime.Scoped);
            builder.Register<TextureManager>(Lifetime.Scoped);
            builder.Register<MaterialManager>(Lifetime.Scoped);
            builder.Register<NodeHandlePool>(Lifetime.Scoped);
            builder.Register<NodeHierarchyUnloader>(Lifetime.Scoped);
            builder.Register<SceneTraverser>(Lifetime.Scoped);
            builder.Register<DynamicNodeLoadCoordinator>(Lifetime.Scoped);
            builder.Register<MapSession>(Lifetime.Scoped);
            builder.Register<StreamingLock>(Lifetime.Scoped)
                .As<IStreamingLock>();
            builder.Register<StreamingPipeline>(Lifetime.Scoped);
        }
    }
}
