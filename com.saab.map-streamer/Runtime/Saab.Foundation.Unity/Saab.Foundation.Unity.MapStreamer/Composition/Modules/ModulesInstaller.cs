using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Streaming;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer.Composition.Modules
{
    internal sealed class ModulesInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<MapShadingModule>();
            builder.RegisterComponentInHierarchy<FoliageModule>()
                .AsSelf()
                .As<IPostTraversal>();
        }
    }
}
