using System;

using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Foundation.Unity.MapStreamer.Runtime;

using UnityEngine;

using VContainer;

using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;

namespace Saab.Foundation.Unity.MapStreamer
{
    [RequireComponent(typeof(NodeEvents))]
    [RequireComponent(typeof(MapStreamerLifetimeScope))]
    public sealed class SceneManager : MonoBehaviour
    {
        private static readonly ProfilerMarker ProfilerMarkerRender =
            new ProfilerMarker(ProfilerCategory.Render, "SM-Render");

        private MapLifecycleController _mapLifecycle;
        private StreamingRuntimeController _runtime;
        private StreamingPipeline _streamingPipeline;
        private RuntimeMapStreamerSettings _settings;

        private bool _initialized;
        private bool _resumeOnEnable;

        [Inject]
        private void Construct(
            MapLifecycleController mapLifecycle,
            StreamingRuntimeController runtime,
            StreamingPipeline streamingPipeline,
            RuntimeMapStreamerSettings settings)
        {
            _mapLifecycle = mapLifecycle;
            _runtime = runtime;
            _streamingPipeline = streamingPipeline;
            _settings = settings;
        }

        public bool Init(bool loadMap = true)
        {
            if (_initialized)
                return true;
            if (!_runtime.Initialize())
                return false;

            _initialized = true;
            if (loadMap && !LoadMap())
            {
                Uninitialize();
                return false;
            }

            return true;
        }

        public bool Uninitialize()
        {
            if (!_initialized)
                return false;

            _runtime.Shutdown();
            _initialized = false;
            return true;
        }

        public bool LoadMap()
        {
            return _mapLifecycle.Load();
        }

        public void ResetMap()
        {
            _mapLifecycle.Reset();
        }

        public void Render()
        {
            if (!_initialized)
                return;

            using (ProfilerMarkerRender.Auto())
            {
                _streamingPipeline.ProcessFrame();
            }
        }

        private void Start()
        {
            Init();
        }

        private void OnEnable()
        {
            ResumeIfNeeded();
        }

        private void OnDisable()
        {
            _resumeOnEnable = _initialized;
            Uninitialize();
        }

        private void OnApplicationQuit()
        {
            Uninitialize();
        }

        private void Update()
        {
            if (_runtime == null || _settings == null)
                return;

            ResumeIfNeeded();

            if (_settings.Options.HasFlag(MapStreamerOptions.RenderInUpdate))
                Render();
        }

        private void ResumeIfNeeded()
        {
            if (!_resumeOnEnable || _initialized || _runtime == null)
                return;

            _resumeOnEnable = false;
            Init();
        }
    }
}
