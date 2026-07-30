using Saab.Foundation.Unity.MapStreamer.ExternalAssets;
using Saab.Foundation.Unity.MapStreamer.Streaming;

using VContainer;
using VContainer.Unity;

namespace Saab.Foundation.Unity.MapStreamer.Composition.ExternalAssets
{
    internal sealed class ExternalAssetsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<ExternalAssetLoader>(Lifetime.Scoped)
                .As<IExternalAssetQueue>()
                .As<IExternalAssetProcessor>()
                .As<IExternalAssetResetter>()
                .As<IExternalAssetRuntime>();
        }
    }
}
