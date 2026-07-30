using System;

using Saab.Foundation.Unity.MapStreamer.Composition.Building;
using Saab.Foundation.Unity.MapStreamer.Composition.Configuration;
using Saab.Foundation.Unity.MapStreamer.Composition.ExternalAssets;
using Saab.Foundation.Unity.MapStreamer.Composition.Gizmo;
using Saab.Foundation.Unity.MapStreamer.Composition.Map;
using Saab.Foundation.Unity.MapStreamer.Composition.Modules;
using Saab.Foundation.Unity.MapStreamer.Composition.Streaming;
using Saab.Foundation.Unity.MapStreamer.Composition.Traversal;

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
        private Modules.MapModuleProfile mapModuleProfile;

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

            new ConfigurationInstaller(
                mapConfig,
                mapStreamerSettings,
                builders).Install(builder);
            new MapServicesInstaller().Install(builder);
            new TraversalInstaller().Install(builder);
            new ExternalAssetsInstaller().Install(builder);
            new BuildingInstaller().Install(builder);
            new ModulesInstaller(mapModuleProfile).Install(builder);
            new GizmoInstaller().Install(builder);
            new StreamingInstaller().Install(builder);
        }
    }
}
