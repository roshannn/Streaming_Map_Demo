using System;

using Saab.Foundation.Unity.MapStreamer.Building.Coordination;
using Saab.Foundation.Unity.MapStreamer.ExternalAssets;
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.GizmoAdapter;
using Saab.Foundation.Unity.MapStreamer.GizmoIntegration;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Nodes.Pooling;
using Saab.Foundation.Unity.MapStreamer.Nodes.Processing;
using Saab.Foundation.Unity.MapStreamer.Runtime;
using Saab.Foundation.Unity.MapStreamer.Streaming;
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

            builder.RegisterInstance(mapConfig)
                .AsSelf()
                .As<IMapConfiguration>();
            builder.RegisterInstance(mapStreamerSettings.CreateRuntimeCopy())
                .AsSelf()
                .As<IStreamingBudget>()
                .As<IStreamingRuntimeOptions>();
            builder.RegisterInstance(builders);

            builder.RegisterComponentInHierarchy<NodeEvents>();
            builder.RegisterComponentInHierarchy<SceneManager>()
                .AsSelf();
            builder.RegisterComponentInHierarchy<CameraControl>()
                .AsSelf()
                .As<IStreamingFrameSource>();
            builder.RegisterComponentInHierarchy<MapShadingModule>();
            builder.RegisterComponentInHierarchy<FoliageModule>()
                .AsSelf()
                .As<IPostTraversal>();

            builder.Register<TraversalConfiguration>(Lifetime.Scoped)
                .As<ITraversalConfiguration>();
            builder.Register<NodeUpdateRegistry>(Lifetime.Scoped)
                .As<INodeUpdateRegistry>()
                .As<INodeUpdateProcessor>();
            builder.Register<ExternalAssetLoader>(Lifetime.Scoped)
                .As<IExternalAssetQueue>()
                .As<IExternalAssetProcessor>()
                .As<IExternalAssetResetter>()
                .As<IExternalAssetRuntime>();
            builder.Register<TraversalNodeFactory>(Lifetime.Scoped)
                .As<ITraversalNodeFactory>();
            builder.Register<GeometryNodeOperations>(Lifetime.Scoped)
                .As<IGeometryNodeOperations>();

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
            builder.Register<SceneTraverser>(Lifetime.Scoped);
            builder.Register<GizmoDynamicLoadCallbacks>(Lifetime.Scoped)
                .As<IGizmoDynamicLoadCallbacks>()
                .As<IStreamedHierarchyRelease>();
            builder.Register<GizmoSceneCallbacks>(Lifetime.Scoped)
                .As<IGizmoSceneCallbacks>();
            builder.Register<GizmoMapCallbacks>(Lifetime.Scoped)
                .As<IGizmoMapCallbacks>();
            builder.Register<GizmoMapDataSource>(Lifetime.Scoped)
                .As<IMapDataSource>();
            builder.Register<GizmoMapInstaller>(Lifetime.Scoped)
                .As<IMapInstaller>();
            builder.Register<GizmoDynamicLoadEventSource>(Lifetime.Scoped)
                .As<IDynamicLoadEventSource>();
            builder.Register<DynamicLoadCoordinator>(Lifetime.Scoped)
                .As<IDynamicLoadPump>();
            builder.Register<GizmoDynamicLoaderRuntime>(Lifetime.Scoped)
                .As<IDynamicLoaderRuntime>();
            builder.Register<GizmoStreamingBackend>(Lifetime.Scoped)
                .AsSelf()
                .As<IStreamingBackend>();
            builder.Register<GizmoStreamingClock>(Lifetime.Scoped)
                .As<IStreamingClock>();
            builder.Register<GizmoStreamingLog>(Lifetime.Scoped)
                .As<IStreamingLog>();

            builder.Register<MapLifecycleController>(Lifetime.Scoped)
                .AsSelf()
                .As<IMapRuntime>();
            builder.Register<StreamingContentResetter>(Lifetime.Scoped)
                .AsSelf()
                .As<IStreamingContentResetter>();
            builder.Register<BuilderLifecycleController>(Lifetime.Scoped)
                .AsSelf()
                .As<IBuilderRuntime>();
            builder.Register<StreamingRuntimeController>(Lifetime.Scoped)
                .AsSelf()
                .As<IStreamingRuntimeState>();
            builder.Register<GizmoStreamingLock>(Lifetime.Scoped)
                .As<IStreamingLock>();
            builder.Register<StreamingPipeline>(Lifetime.Scoped);
        }
    }
}
