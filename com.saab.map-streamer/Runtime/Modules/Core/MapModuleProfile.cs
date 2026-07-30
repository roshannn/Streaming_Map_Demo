using System;
using System.Collections.Generic;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [CreateAssetMenu(
        fileName = "MapModuleProfile",
        menuName = "Saab/Map Streamer/Module Profile")]
    public sealed class MapModuleProfile : ScriptableObject
    {
        [SerializeField]
        private MapModuleDefinition[] modules =
            Array.Empty<MapModuleDefinition>();

        public IReadOnlyList<MapModuleDefinition> Modules => modules;

        public void RegisterModules(IMapModuleRegistrar registrar)
        {
            if (registrar == null)
                throw new ArgumentNullException(nameof(registrar));

            foreach (var module in modules)
            {
                if (module != null && module.Enabled)
                    registrar.Register(module);
            }
        }
    }
}
