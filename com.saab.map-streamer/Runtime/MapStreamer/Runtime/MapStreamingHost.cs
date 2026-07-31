using System;

using Saab.Foundation.Unity.MapStreamer.GizmoAdapter;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Foundation.Unity.MapStreamer.Runtime;

using UnityEngine;

using VContainer;

namespace Saab.Foundation.Unity.MapStreamer
{
    public sealed class MapStreamingHost : MonoBehaviour
    {
        private MapLifecycleController _mapLifecycle;
        private StreamingRuntimeController _runtime;
        private StreamingPipeline _streamingPipeline;
        private RuntimeMapStreamerSettings _settings;

        private bool _initialized;
        private bool _resumeOnEnable;
        private IDisposable _applicationShutdownRegistration;

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

        public bool Init()
        {
            if (_initialized)
                return true;
            if (!_runtime.Initialize())
                return false;

            _initialized = true;
            if (!_mapLifecycle.LoadMap())
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


        public void Render()
        {
            if (!_initialized)
                return;

            _streamingPipeline.ProcessFrame();
        }

        private void Start()
        {
            Init();
        }

        private void OnEnable()
        {
            RegisterApplicationShutdown();
            ResumeIfNeeded();
        }

        private void OnDisable()
        {
            _resumeOnEnable = _initialized;
            try
            {
                Uninitialize();
            }
            finally
            {
                UnregisterApplicationShutdown();
            }
        }

        private void OnDestroy()
        {
            UnregisterApplicationShutdown();
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

        private void RegisterApplicationShutdown()
        {
            if (_applicationShutdownRegistration != null)
                return;

            _applicationShutdownRegistration =
                GizmoApplicationLifetime.RegisterShutdown(
                    () => Uninitialize());
        }

        private void UnregisterApplicationShutdown()
        {
            _applicationShutdownRegistration?.Dispose();
            _applicationShutdownRegistration = null;
        }
    }
}
