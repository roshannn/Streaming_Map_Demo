using Saab.Foundation.Unity.MapStreamer.Streaming;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer.Composition.Configuration
{
    internal sealed class ConfigurationInstaller : IInstaller
    {
        private readonly MapConfig _mapConfig;
        private readonly MapStreamerSettings _settings;
        private readonly NodeBuilderBase[] _builders;

        public ConfigurationInstaller(
            MapConfig mapConfig,
            MapStreamerSettings settings,
            NodeBuilderBase[] builders)
        {
            _mapConfig = mapConfig;
            _settings = settings;
            _builders = builders;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_mapConfig)
                .AsSelf()
                .As<IMapConfiguration>();
            builder.RegisterInstance(_settings.CreateRuntimeCopy())
                .AsSelf()
                .As<IStreamingBudget>()
                .As<IStreamingRuntimeOptions>();
            builder.RegisterInstance(_builders);
        }
    }
}
