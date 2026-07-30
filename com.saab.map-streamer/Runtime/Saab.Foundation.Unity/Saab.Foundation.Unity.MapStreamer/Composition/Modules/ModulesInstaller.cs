using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Streaming;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer.Composition.Modules
{
    internal sealed class ModulesInstaller : IInstaller
    {
        private readonly MapModuleProfile _profile;

        public ModulesInstaller(MapModuleProfile profile)
        {
            _profile = profile;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<MapShadingModule>()
                .AsSelf()
                .As<IMapModule>()
                .As<ITerrainAddedHandler>();
            builder.RegisterComponentInHierarchy<FoliageModule>()
                .AsSelf()
                .As<IMapModule>()
                .As<ITerrainAddedHandler>()
                .As<ITerrainRemovedHandler>()
                .As<IStreamingFrameCompletedHandler>();
            var catalog = new MapModuleCatalog();
            _profile?.RegisterModules(catalog);
            builder.RegisterInstance(catalog);
            builder.Register<TerrainModuleContextFactory>(Lifetime.Scoped);
            builder.Register<MapModuleRuntime>(Lifetime.Scoped)
                .AsSelf()
                .As<IMapModuleRuntime>()
                .As<IStreamingFrameCompletionSink>();
        }
    }
}
