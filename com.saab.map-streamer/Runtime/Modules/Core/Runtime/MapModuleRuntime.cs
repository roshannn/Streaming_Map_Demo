using System;
using System.Collections.Generic;

using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

using UnityEngine;
using VContainer;

namespace Saab.Foundation.Unity.MapStreamer.Modules.Runtime
{
    internal sealed class MapModuleRuntime :
        IMapModuleRuntime,
        IStreamingFrameCompletionSink
    {
        private readonly NodeEvents _nodeEvents;
        private readonly TerrainModuleContextFactory _contextFactory;
        private readonly IStreamingLog _log;
        private readonly IReadOnlyList<IMapModule> _legacyModules;
        private readonly MapModuleCatalog _catalog;
        private readonly IMapModuleServices _services;
        private IReadOnlyList<IMapModule> _modules;
        private readonly Dictionary<GameObject, TerrainModuleContext>
            _terrainRegistrations =
                new Dictionary<GameObject, TerrainModuleContext>();

        private int _initializedModuleCount;

        public MapModuleRuntime(
            NodeEvents nodeEvents,
            TerrainModuleContextFactory contextFactory,
            IStreamingLog log,
            IEnumerable<IMapModule> modules,
            MapModuleCatalog catalog,
            IObjectResolver resolver)
        {
            _nodeEvents = nodeEvents;
            _contextFactory = contextFactory;
            _log = log;
            _legacyModules = ToList(modules);
            _catalog = catalog;
            _services = new MapModuleServices(resolver);
            _modules = Array.Empty<IMapModule>();
        }

        public bool IsInitialized { get; private set; }
        internal int ActiveTerrainCount => _terrainRegistrations.Count;
        internal int AddedTerrainCount { get; private set; }
        internal int RemovedTerrainCount { get; private set; }
        internal int InvalidContextCount { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
                return;

            try
            {
                if (_modules.Count == 0)
                    _modules = CreateModules();

                for (; _initializedModuleCount < _modules.Count;
                     ++_initializedModuleCount)
                {
                    _modules[_initializedModuleCount].Initialize();
                }

                _nodeEvents.TerrainCreated += OnTerrainCreated;
                _nodeEvents.TerrainRemoved += OnTerrainRemoved;
                IsInitialized = true;
            }
            catch
            {
                ShutdownInitializedModules();
                throw;
            }
        }

        public void Shutdown()
        {
            if (!IsInitialized && _initializedModuleCount == 0)
                return;

            _nodeEvents.TerrainCreated -= OnTerrainCreated;
            _nodeEvents.TerrainRemoved -= OnTerrainRemoved;
            IsInitialized = false;

            if (_terrainRegistrations.Count > 0)
            {
                foreach (var terrain in _terrainRegistrations.Values)
                    DispatchTerrainRemoved(in terrain);
                _terrainRegistrations.Clear();
            }

            ShutdownInitializedModules();
        }

        public void OnFrameCompleted(
            in StreamingFrameCompletionContext context)
        {
            if (!IsInitialized)
                return;

            var moduleContext = new StreamingFrameModuleContext(
                context.RenderTime,
                context.Elapsed);
            var mapEvent = new StreamingFrameCompletedEvent(
                in moduleContext);
            foreach (var module in _modules)
            {
                if (module is
                    IMapEventHandler<StreamingFrameCompletedEvent> handler)
                    TryDispatch(
                        handler,
                        () => handler.Handle(in mapEvent));
                else if (module is
                         IStreamingFrameCompletedHandler legacyHandler)
                    TryDispatch(
                        legacyHandler,
                        () => legacyHandler.OnStreamingFrameCompleted(
                            in moduleContext));
            }
        }

        private void OnTerrainCreated(GameObject gameObject, bool isAsset)
        {
            if (!IsInitialized)
                return;

            if (!_contextFactory.TryCreate(
                    gameObject,
                    isAsset,
                    out var context,
                    out var failure))
            {
                ++InvalidContextCount;
                _log.Write(
                    StreamingLogLevel.Warning,
                    $"Module terrain registration skipped: {failure}");
                return;
            }

            if (_terrainRegistrations.TryGetValue(
                    gameObject,
                    out var previous))
            {
                DispatchTerrainRemoved(in previous);
            }

            _terrainRegistrations[gameObject] = context;
            ++AddedTerrainCount;
            var mapEvent = new TerrainAddedEvent(in context);
            foreach (var module in _modules)
            {
                if (module is IMapEventHandler<TerrainAddedEvent> handler)
                    TryDispatch(
                        handler,
                        () => handler.Handle(in mapEvent));
                else if (module is ITerrainAddedHandler legacyHandler)
                    TryDispatch(
                        legacyHandler,
                        () => legacyHandler.OnTerrainAdded(in context));
            }
        }

        private void OnTerrainRemoved(
            GameObject gameObject,
            byte nodeVersion)
        {
            if (!IsInitialized)
                return;

            if (!_terrainRegistrations.TryGetValue(
                    gameObject,
                    out var context))
            {
                ++InvalidContextCount;
                _log.Write(
                    StreamingLogLevel.Warning,
                    "Module terrain removal had no active registration " +
                    $"[object={gameObject?.GetInstanceID()}].");
                return;
            }

            if (context.Identity.NodeVersion != nodeVersion)
            {
                ++InvalidContextCount;
                _log.Write(
                    StreamingLogLevel.Warning,
                    "Stale module terrain removal ignored " +
                    $"[expected={context.Identity}, " +
                    $"actual={gameObject.GetInstanceID()}:{nodeVersion}].");
                return;
            }

            _terrainRegistrations.Remove(gameObject);
            ++RemovedTerrainCount;
            DispatchTerrainRemoved(in context);
        }

        private void DispatchTerrainRemoved(
            in TerrainModuleContext terrain)
        {
            var removal = new TerrainRemovalContext(in terrain);
            var mapEvent = new TerrainRemovedEvent(in removal);
            foreach (var module in _modules)
            {
                if (module is IMapEventHandler<TerrainRemovedEvent> handler)
                    TryDispatch(
                        handler,
                        () => handler.Handle(in mapEvent));
                else if (module is ITerrainRemovedHandler legacyHandler)
                    TryDispatch(
                        legacyHandler,
                        () => legacyHandler.OnTerrainRemoved(in removal));
            }
        }

        private void ShutdownInitializedModules()
        {
            while (_initializedModuleCount > 0)
            {
                var module = _modules[--_initializedModuleCount];
                TryDispatch(module, module.Shutdown);
            }
        }

        private void TryDispatch(object handler, Action callback)
        {
            try
            {
                callback();
            }
            catch (Exception exception)
            {
                _log.Write(
                    StreamingLogLevel.Warning,
                    $"Map module {handler.GetType().FullName} failed: " +
                    exception);
            }
        }

        private IReadOnlyList<IMapModule> CreateModules()
        {
            var definitions = _catalog.GetOrderedDefinitions();
            if (definitions.Count == 0)
                return _legacyModules;

            var modules = new List<IMapModule>(definitions.Count);
            foreach (var definition in definitions)
            {
                var module = definition.CreateRuntime(_services);
                if (module == null)
                    throw new InvalidOperationException(
                        $"Map module '{definition.ModuleId}' returned no runtime.");
                modules.Add(module);
            }

            return modules;
        }

        private static IReadOnlyList<T> ToList<T>(IEnumerable<T> values) =>
            values is IReadOnlyList<T> list
                ? list
                : new List<T>(values);
    }
}
