using System;
using System.Collections.Generic;
using System.Linq;

using VContainer;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    internal sealed class MapModuleServices : IMapModuleServices
    {
        private readonly IObjectResolver _resolver;

        public MapModuleServices(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public T Get<T>() => _resolver.Resolve<T>();
    }

    internal sealed class MapModuleCatalog : IMapModuleRegistrar
    {
        private readonly List<MapModuleDefinition> _definitions =
            new List<MapModuleDefinition>();

        public IReadOnlyList<MapModuleDefinition> Definitions => _definitions;

        public void Register(MapModuleDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (!definition.TryValidate(out var failure))
                throw new InvalidOperationException(failure);

            _definitions.Add(definition);
        }

        public IReadOnlyList<MapModuleDefinition> GetOrderedDefinitions()
        {
            var byId = new Dictionary<string, MapModuleDefinition>(
                StringComparer.Ordinal);
            foreach (var definition in _definitions)
            {
                if (byId.ContainsKey(definition.ModuleId))
                    throw new InvalidOperationException(
                        $"Duplicate map module ID '{definition.ModuleId}'.");
                byId.Add(definition.ModuleId, definition);
            }

            foreach (var definition in _definitions)
            {
                foreach (var dependency in definition.Dependencies)
                {
                    if (!byId.ContainsKey(dependency))
                        throw new InvalidOperationException(
                            $"Map module '{definition.ModuleId}' requires " +
                            $"missing module '{dependency}'.");
                }
            }

            var ordered = new List<MapModuleDefinition>(_definitions.Count);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);

            foreach (var definition in _definitions
                         .OrderBy(value => value.ExecutionOrder))
            {
                Visit(definition, byId, visiting, visited, ordered);
            }

            return ordered;
        }

        private static void Visit(
            MapModuleDefinition definition,
            IReadOnlyDictionary<string, MapModuleDefinition> byId,
            ISet<string> visiting,
            ISet<string> visited,
            ICollection<MapModuleDefinition> ordered)
        {
            if (visited.Contains(definition.ModuleId))
                return;
            if (!visiting.Add(definition.ModuleId))
                throw new InvalidOperationException(
                    $"Circular map module dependency at " +
                    $"'{definition.ModuleId}'.");

            foreach (var dependency in definition.Dependencies)
                Visit(byId[dependency], byId, visiting, visited, ordered);

            visiting.Remove(definition.ModuleId);
            visited.Add(definition.ModuleId);
            ordered.Add(definition);
        }
    }
}
