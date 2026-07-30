using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.Runtime;
using Saab.Foundation.Unity.MapStreamer.Streaming;

using VContainer;
using VContainer.Unity;

#pragma warning disable 0618
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
            if (_profile == null)
            {
                builder.RegisterComponentInHierarchy<FoliageModule>()
                    .AsSelf();
                builder.Register<FoliageCompatibilityRuntime>(
                        Lifetime.Scoped)
                    .As<IMapModule>();
            }
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
#pragma warning restore 0618
