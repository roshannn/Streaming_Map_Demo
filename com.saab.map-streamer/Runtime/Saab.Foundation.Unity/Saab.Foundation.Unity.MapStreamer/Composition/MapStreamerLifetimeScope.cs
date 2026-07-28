using System;

using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.Building.Coordination;
using Saab.Foundation.Unity.MapStreamer.ExternalAssets;
using Saab.Foundation.Unity.MapStreamer.Maps;
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Nodes.Pooling;
using Saab.Foundation.Unity.MapStreamer.Nodes.Processing;
using Saab.Foundation.Unity.MapStreamer.Runtime;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Streaming.Native;
using Saab.Foundation.Unity.MapStreamer.Streaming.Pipeline;
using Saab.Foundation.Unity.MapStreamer.Streaming.Synchronization;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Contracts;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Foundation.Unity.MapStreamer.Traversal.Operations;

using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer
{
    public sealed class MapStreamerLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private MapConfig mapConfig;

        [SerializeField]
        private MapStreamerSettings mapStreamerSettings;

        [SerializeField]
        private NodeBuilderBase[] builders = Array.Empty<NodeBuilderBase>();

        protected override void Configure(IContainerBuilder builder)
        {
            if (mapConfig == null)
                throw new InvalidOperationException(
                    "MapConfig must be assigned on MapStreamerLifetimeScope.");
            if (mapStreamerSettings == null)
                throw new InvalidOperationException(
                    "MapStreamerSettings must be assigned on MapStreamerLifetimeScope.");

            builder.RegisterInstance(mapConfig);
            builder.RegisterInstance(mapStreamerSettings.CreateRuntimeCopy());
            builder.RegisterInstance(builders);

            builder.RegisterComponentInHierarchy<NodeEvents>();
            builder.RegisterComponentInHierarchy<SceneManager>()
                .AsSelf()
                .As<IPostTraversalEvents>();
            builder.RegisterComponentInHierarchy<CameraControl>();
            builder.RegisterComponentInHierarchy<MapShadingModule>();
            builder.RegisterComponentInHierarchy<FoliageModule>();

            builder.Register<TraversalConfiguration>(Lifetime.Scoped)
                .As<ITraversalConfiguration>();
            builder.Register<NodeUpdateRegistry>(Lifetime.Scoped)
                .As<INodeUpdateRegistry>();
            builder.Register<ExternalAssetLoader>(Lifetime.Scoped)
                .As<IExternalAssetQueue>()
                .As<IExternalAssetProcessor>()
                .As<IExternalAssetResetter>();
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
            builder.Register<GizmoDynamicLoaderController>(Lifetime.Scoped);
            builder.Register<NativeSceneResources>(Lifetime.Scoped);

            builder.Register<MapLifecycleController>(Lifetime.Scoped);
            builder.Register<StreamingContentResetter>(Lifetime.Scoped);
            builder.Register<BuilderLifecycleController>(Lifetime.Scoped);
            builder.Register<StreamingRuntimeController>(Lifetime.Scoped)
                .AsSelf()
                .As<IStreamingRuntimeState>();
            builder.Register<StreamingLock>(Lifetime.Scoped)
                .As<IStreamingLock>();
            builder.Register<StreamingPipeline>(Lifetime.Scoped);
        }
    }
}
