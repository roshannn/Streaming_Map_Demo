using Saab.Foundation.Unity.MapStreamer.Building.Coordination;
using Saab.Foundation.Unity.MapStreamer.Runtime;
using Saab.Foundation.Unity.MapStreamer.Streaming;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer.Composition.Streaming
{
    internal sealed class StreamingInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<MapStreamingHost>()
                .AsSelf();
            builder.RegisterComponentInHierarchy<CameraControl>()
                .AsSelf()
                .As<IStreamingFrameSource>();
            builder.Register<DynamicLoadCoordinator>(Lifetime.Scoped)
                .As<IDynamicLoadPump>();
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
            builder.Register<StreamingPipeline>(Lifetime.Scoped);
        }
    }
}
