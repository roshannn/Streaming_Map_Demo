using Saab.Foundation.Map;

#pragma warning disable 0618
namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime
{
    /// <summary>
    /// Adapts a legacy scene component into the profile-independent runtime.
    /// The map-module catalog ignores this runtime whenever a profile exists.
    /// </summary>
    internal sealed class FoliageCompatibilityRuntime : IMapModule,
        IMapEventHandler<TerrainAddedEvent>,
        IMapEventHandler<TerrainRemovedEvent>,
        IMapEventHandler<StreamingFrameCompletedEvent>
    {
        private readonly FoliageModule _facade;
        private readonly CameraControl _cameraControl;
        private readonly IMapCoordinates _mapCoordinates;
        private FoliageModuleRuntime _runtime;

        public FoliageCompatibilityRuntime(
            FoliageModule facade,
            CameraControl cameraControl,
            IMapCoordinates mapCoordinates)
        {
            _facade = facade;
            _cameraControl = cameraControl;
            _mapCoordinates = mapCoordinates;
        }

        public void Initialize()
        {
            if (_runtime != null)
                return;
            _runtime = new FoliageModuleRuntime(
                _facade.Snapshot(),
                _cameraControl,
                _mapCoordinates);
            try
            {
                _runtime.Initialize();
            }
            catch
            {
                _runtime = null;
                throw;
            }
        }

        public void Shutdown()
        {
            _runtime?.Shutdown();
            _runtime = null;
        }

        public void Handle(in TerrainAddedEvent mapEvent)
        {
            if (_runtime != null)
                _runtime.Handle(in mapEvent);
        }

        public void Handle(in TerrainRemovedEvent mapEvent)
        {
            if (_runtime != null)
                _runtime.Handle(in mapEvent);
        }

        public void Handle(in StreamingFrameCompletedEvent mapEvent)
        {
            if (_runtime != null)
                _runtime.Handle(in mapEvent);
        }
    }
}
#pragma warning restore 0618
