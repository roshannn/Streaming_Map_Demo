using System;
using System.Collections.Generic;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    public readonly struct MapLoadFailure
    {
        public MapLoadFailure(string message, int nativeErrorCode)
        {
            Message = message;
            NativeErrorCode = nativeErrorCode;
        }

        public string Message { get; }
        public int NativeErrorCode { get; }
    }

    public delegate void MapLoadErrorHandler(
        ref string url,
        MapLoadFailure failure,
        ref bool retry);

    public enum DynamicLoadState
    {
        Loaded,
        Unloaded,
    }

    public interface INativeNodeHandle : IDisposable
    {
        long Identity { get; }
        GameObject Traverse();
        bool TryFindGameObjects(out IReadOnlyList<GameObject> gameObjects);
    }

    public interface INativeLoaderHandle : IDisposable
    {
        long Identity { get; }
        Transform FindAnchor();
        bool TryFindGameObjects(
            out IReadOnlyList<GameObject> gameObjects);
    }

    public readonly struct DynamicLoadEvent
    {
        public DynamicLoadEvent(
            DynamicLoadState state,
            INativeLoaderHandle loader,
            INativeNodeHandle node)
        {
            State = state;
            Loader = loader;
            Node = node;
        }

        public DynamicLoadState State { get; }
        public INativeLoaderHandle Loader { get; }
        public INativeNodeHandle Node { get; }
    }

    public enum NodeActivationState
    {
        Traversable,
        NotTraversable,
    }

    public readonly struct NodeActivationEvent
    {
        public NodeActivationEvent(
            NodeActivationState state,
            INativeNodeHandle node)
        {
            State = state;
            Node = node;
        }

        public NodeActivationState State { get; }
        public INativeNodeHandle Node { get; }
    }

    public interface IDynamicLoadEventSource
    {
        event Action<DynamicLoadEvent> LoadChanged;
        event Action<NodeActivationEvent> ActivationChanged;
        void Subscribe();
        void Unsubscribe();
    }

    public interface IStreamedHierarchyRelease
    {
        void ReleaseChildren(Transform root);
    }

    public interface IStreamingBackend
    {
        bool IsInitialized { get; }
        bool Initialize();
        void Shutdown();
        void Render(in StreamingFrame frame);
    }

    public interface IStreamingRuntimeState
    {
        bool IsInitialized { get; }
    }

    public interface IMapModuleRuntime
    {
        bool IsInitialized { get; }
        void Initialize();
        void Shutdown();
    }

    public interface IStreamingClock
    {
        double SystemSeconds { get; }
    }

    public interface IStreamingFrameSource
    {
        bool IsAvailable { get; }
        bool TryCreateFrame(double renderTime, out StreamingFrame frame);
    }

    public interface IStreamingLock
    {
        bool IsRenderLock { get; }
        bool IsOwnedByCurrentThread { get; }
        void AcquireEdit();
        bool TryAcquireEdit(uint timeoutMilliseconds);
        bool ChangeToRender();
        bool ChangeToEdit();
        void Release();
    }

    public interface IDynamicLoadPump
    {
        bool HasPendingLoads { get; }
        void Subscribe();
        void Unsubscribe();
        void ProcessLoads();
        void ProcessActivations();
        void Reset();
    }

    public interface INodePoolMaintenance
    {
        void ProcessPending(int count);
        void PreAllocate(int count, TimeSpan timeBudget);
    }

    public interface IBuildScheduler
    {
        void Process(TimeSpan maxBuildTime);
    }

    public interface INodeUpdateProcessor
    {
        void UpdateNodes();
    }

    public readonly struct StreamingFrameCompletionContext
    {
        public StreamingFrameCompletionContext(
            double renderTime,
            TimeSpan elapsed)
        {
            RenderTime = renderTime;
            Elapsed = elapsed;
        }

        public double RenderTime { get; }
        public TimeSpan Elapsed { get; }
    }

    public interface IStreamingFrameCompletionSink
    {
        void OnFrameCompleted(in StreamingFrameCompletionContext context);
    }

    public interface IStreamingBudget
    {
        double MinimumBuildTime { get; }
        double MaximumBuildTime { get; }
    }

    public enum StreamingLogLevel
    {
        Debug,
        Warning,
        Fatal,
    }

    public interface IStreamingLog
    {
        void Write(StreamingLogLevel level, string message);
    }

    public interface IDynamicLoaderRuntime
    {
        void Start(byte loaderCount);
        void Stop();
    }

    public interface IBuilderRuntime
    {
        bool SupportsInstancing { get; }
        void Initialize();
    }

    public interface IStreamingRuntimeOptions
    {
        byte DynamicLoaderCount { get; }
        void DisableInstancing();
    }

    public interface IExternalAssetRuntime
    {
        void StartProcessing();
        void StopProcessing();
    }

    public interface IMapRuntime
    {
        void Reset();
    }

    public interface IMapConfiguration
    {
        string MapUrl { get; }
    }

    public interface INativeMapHandle : IDisposable
    {
        long Identity { get; }
    }

    public interface IMapDataSource
    {
        bool TryLoad(
            string url,
            out INativeMapHandle map,
            out MapLoadFailure failure);
    }

    public interface IMapInstaller
    {
        GameObject Install(string url, INativeMapHandle map);
    }

    public interface IStreamingContentResetter
    {
        void Reset(GameObject root);
    }
}
