using Saab.Foundation.Unity.MapStreamer.GizmoAdapter;
using Saab.Foundation.Unity.MapStreamer.GizmoIntegration;
using Saab.Foundation.Unity.MapStreamer.Streaming;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer.Composition.Gizmo
{
    internal sealed class GizmoInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
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
            builder.Register<GizmoDynamicLoaderRuntime>(Lifetime.Scoped)
                .As<IDynamicLoaderRuntime>();
            builder.Register<GizmoStreamingBackend>(Lifetime.Scoped)
                .AsSelf()
                .As<IStreamingBackend>();
            builder.Register<GizmoStreamingClock>(Lifetime.Scoped)
                .As<IStreamingClock>();
            builder.Register<GizmoStreamingLog>(Lifetime.Scoped)
                .As<IStreamingLog>();
            builder.Register<GizmoStreamingLock>(Lifetime.Scoped)
                .As<IStreamingLock>();
        }
    }
}
