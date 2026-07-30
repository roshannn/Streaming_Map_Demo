using Saab.Foundation.Map;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer.Composition.Map
{
    internal sealed class MapServicesInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<MapSession>(Lifetime.Scoped)
                .AsSelf()
                .As<IMapSession>();
            builder.Register<MapCoordinates>(Lifetime.Scoped)
                .As<IMapCoordinates>();
            builder.Register<MapViewContext>(Lifetime.Scoped)
                .AsSelf()
                .As<IMapViewContext>();
            builder.Register<MapSurfaceQueries>(Lifetime.Scoped)
                .As<IMapSurfaceQueries>();
        }
    }
}
