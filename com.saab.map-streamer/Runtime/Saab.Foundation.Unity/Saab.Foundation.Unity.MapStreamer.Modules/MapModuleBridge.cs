using System;
using System.Collections.Generic;

using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    internal sealed class MapModuleBridge :
        IMapModuleRuntime,
        IStreamingFrameCompletionSink
    {
        private readonly NodeEvents _nodeEvents;
        private readonly TerrainModuleContextFactory _contextFactory;
        private readonly IStreamingLog _log;
        private readonly IReadOnlyList<IMapModule> _modules;
        private readonly IReadOnlyList<ITerrainAddedHandler> _terrainAdded;
        private readonly IReadOnlyList<ITerrainRemovedHandler> _terrainRemoved;
        private readonly IReadOnlyList<IStreamingFrameCompletedHandler>
            _frameCompleted;
        private readonly Dictionary<GameObject, TerrainModuleContext>
            _terrainRegistrations =
                new Dictionary<GameObject, TerrainModuleContext>();

        private int _initializedModuleCount;

        public MapModuleBridge(
            NodeEvents nodeEvents,
            TerrainModuleContextFactory contextFactory,
            IStreamingLog log,
            IEnumerable<IMapModule> modules,
            IEnumerable<ITerrainAddedHandler> terrainAdded,
            IEnumerable<ITerrainRemovedHandler> terrainRemoved,
            IEnumerable<IStreamingFrameCompletedHandler> frameCompleted)
        {
            _nodeEvents = nodeEvents;
            _contextFactory = contextFactory;
            _log = log;
            _modules = ToList(modules);
            _terrainAdded = ToList(terrainAdded);
            _terrainRemoved = ToList(terrainRemoved);
            _frameCompleted = ToList(frameCompleted);
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
            foreach (var handler in _frameCompleted)
            {
                TryDispatch(
                    handler,
                    () => handler.OnStreamingFrameCompleted(
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
            foreach (var handler in _terrainAdded)
            {
                TryDispatch(
                    handler,
                    () => handler.OnTerrainAdded(in context));
            }
        }

        private void OnTerrainRemoved(GameObject gameObject)
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

            _terrainRegistrations.Remove(gameObject);
            ++RemovedTerrainCount;
            DispatchTerrainRemoved(in context);
        }

        private void DispatchTerrainRemoved(
            in TerrainModuleContext terrain)
        {
            var removal = new TerrainRemovalContext(in terrain);
            foreach (var handler in _terrainRemoved)
            {
                TryDispatch(
                    handler,
                    () => handler.OnTerrainRemoved(in removal));
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

        private static IReadOnlyList<T> ToList<T>(IEnumerable<T> values) =>
            values is IReadOnlyList<T> list
                ? list
                : new List<T>(values);
    }
}
