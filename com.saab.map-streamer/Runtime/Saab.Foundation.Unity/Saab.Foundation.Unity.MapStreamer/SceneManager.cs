//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s) 
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied. 
//
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: SceneManager.cs
// Module		:
// Description	: Management of dynamic asset loader from GizmoSDK
// Author		: Anders Modén
// Product		: Gizmo3D 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...
//
// Who	Date	Description
//
// AMO	180607	Created file        (2.9.1)
// AMO  200304  Updated SceneManager with events for external users     (2.10.1)
// AMO  221130  Updated SM with new locking and camera sync             (2.12.35)
//
//******************************************************************************

//#define DEBUG_CAMERA

// Unity Managed classes
using UnityEngine;
using UnityEngine.Serialization;

// Gizmo Managed classes
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

// Map utility
using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Streaming;
using Saab.Foundation.Unity.MapStreamer.Streaming.Pipeline;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;
using Saab.Utility.GfxCaps;

// System
using System;

using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using VContainer;

namespace Saab.Foundation.Unity.MapStreamer
{
    // The SceneManager behaviour takes a unity camera and follows that to populate the current scene with GameObjects in a scenegraph hierarchy

    public interface ISceneManagerCamera
    {
        UnityEngine.Camera Camera { get; }
        Vec3D GlobalPosition { get; set; }           // Position in Global coordinate system

        Vector3 Up { get; }                         // Get up vector in global coordinate system for current position
        Vector3 North { get; }                      // Get north vector in global coordinate system for current position

        void PreTraverse(bool locked);              // Executed before scene is traversed and updated with new transform and new geometry

        void PostTraverse(bool locked);             // Executed after nodes are repositioned with new transforms and correct activations

        double UpdateCamera(double renderTime);     // Executed just before camera transform is used. Update you cam animation in this

        void MapChanged();                          // Executed when map is changed

        float LodFactor { get; }                    // Current lod factor
    }

    /// <summary>
    /// Options for configuring SceneManager runtime behaviour
    /// </summary>
    [Flags]
    public enum SceneManagerOptions
    {
        None = 0,

        /// <summary>
        /// Render during component update, disable to manually control when render is performed
        /// </summary>
        RenderInUpdate = 1 << 0,

        /// <summary>
        /// Skip asset loading and ignore RefNodes
        /// </summary>
        DisableInstancing = 1 << 1,

        LazyLoadAssets = 1 << 2,
    }

    [Serializable]
    public struct SceneManagerSettings
    {
        public double   MaxBuildTime;                       // Max time to spend in frame to build objects
        public double   MinBuildTime;                       // Min time to spend in frame to build objects
        public byte     DynamicLoaders;
        public IntersectMaskValue IntersectMask;
        public SceneManagerOptions Options;

        public static readonly SceneManagerSettings Default = new SceneManagerSettings
        {
            MaxBuildTime = 0.012,       // 12ms
            MinBuildTime = 0.004,       // 4ms == 16 ms, 60fps
            DynamicLoaders = 4,
            IntersectMask = IntersectMaskValue.ALL,
            Options = SceneManagerOptions.RenderInUpdate,
        };
    }


    [RequireComponent(typeof(NodeEvents))]
    [RequireComponent(typeof(MapStreamerLifetimeScope))]
    public class SceneManager : MonoBehaviour
    {
        [FormerlySerializedAs("Settings")]
        [SerializeField]
        private SceneManagerSettings _settings = SceneManagerSettings.Default;

        [FormerlySerializedAs("Builders")]
        [SerializeField]
        private NodeBuilderBase[] _builders = Array.Empty<NodeBuilderBase>();

        public SceneManagerSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        public ISceneManagerCamera  SceneManagerCamera { get; private set; }

        // Events ----------------------------------------------------------

        public delegate void EventHandler_OnNode(Node node);
        public delegate void EventHandler_OnUpdateCamera(double renderTime);
        public delegate void EventHandler_Traverse(bool locked);    // Pre and Post traversal in locked or unlocked mode (edit)

        public event EventHandler_Traverse          OnPreTraverse;  // Called before SceneManagerCamera is updated
        public event EventHandler_Traverse          OnPostTraverse; // Called after SceneManagerCamera is updated
        public event EventHandler_OnNode            OnMapChanged;
        public event MapLoadErrorHandler            OnMapLoadError;
        public event EventHandler_OnUpdateCamera    OnUpdateCamera; // Called after SceneManagerCamera is updated

        #region ------------- Privates ----------------

        private CullTraverseAction _nativeTraverseAction;

        #endregion


        private static readonly ProfilerMarker ProfilerMarkerRender =
            new ProfilerMarker(ProfilerCategory.Render, "SM-Render");
        private bool _initialized;
        private bool _resumeOnEnable;

        private IExternalAssetQueue _externalAssetLoader;
        private GeometryBuilderRegistry _builderRegistry;
        private PooledNodeObjectPolicyRegistry _poolPolicyRegistry;

        // Used by builders to share and manage texture resources
        private TextureManager _textureManager;

        // Used by builders to share and manage Material resources
        private MaterialManager _materialManager;

        private SceneTraverser _sceneTraverser;
        private DynamicNodeLoadCoordinator _dynamicNodeLoadCoordinator;
        private NodeHandlePool _nodeHandlePool;
        private ITraversalConfiguration _traversalConfiguration;
        private StreamingPipeline _streamingPipeline;
        private NativeSceneResources _nativeScene;
        private MapLifecycleController _mapLifecycle;
        private MapConfig _mapConfig;

        [Inject]
        private void Construct(
            IExternalAssetQueue externalAssetLoader,
            GeometryBuilderRegistry builderRegistry,
            PooledNodeObjectPolicyRegistry poolPolicyRegistry,
            TextureManager textureManager,
            MaterialManager materialManager,
            SceneTraverser sceneTraverser,
            DynamicNodeLoadCoordinator dynamicNodeLoads,
            NodeHandlePool nodeHandlePool,
            ITraversalConfiguration traversalConfiguration,
            StreamingPipeline streamingPipeline,
            NativeSceneResources nativeScene,
            MapLifecycleController mapLifecycle,
            MapConfig mapConfig,
            ISceneManagerCamera sceneManagerCamera)
        {
            _externalAssetLoader = externalAssetLoader;
            _builderRegistry = builderRegistry;
            _poolPolicyRegistry = poolPolicyRegistry;
            _textureManager = textureManager;
            _materialManager = materialManager;
            _sceneTraverser = sceneTraverser;
            _dynamicNodeLoadCoordinator = dynamicNodeLoads;
            _nodeHandlePool = nodeHandlePool;
            _traversalConfiguration = traversalConfiguration;
            _streamingPipeline = streamingPipeline;
            _nativeScene = nativeScene;
            _mapLifecycle = mapLifecycle;
            _mapConfig = mapConfig;
            SceneManagerCamera = sceneManagerCamera;
        }

        public void AddBuilder(INodeBuilder builder)
        {
            if (_initialized)
                throw new InvalidOperationException("builders must be registered before init");
            
            RegisterBuilder(builder);
        }

        public void RemoveBuilder(INodeBuilder builder)
        {
            _builderRegistry.Remove(builder);
            if (builder is IPooledNodeObjectPolicy policy)
                _poolPolicyRegistry.Remove(policy);
        }

        private void RegisterBuilder(INodeBuilder builder)
        {
            if (!(builder is IPooledNodeObjectPolicy policy))
            {
                throw new ArgumentException(
                    $"{builder.GetType().Name} must provide a pooled-object " +
                    $"policy for feature {builder.Feature}",
                    nameof(builder));
            }

            _builderRegistry.Add(builder);
            _poolPolicyRegistry.Add(policy);
        }

        public bool LoadMap()
        {
            var loadErrorHandler = OnMapLoadError;
            if (_mapLifecycle.Load(
                    _mapConfig.MapUrl,
                    loadErrorHandler,
                    out var node))
            {
                if (SceneManagerCamera != null)
                    SceneManagerCamera.MapChanged();

                OnMapChanged?.Invoke(node);
                return true;
            }

            return false;
        }

        public void ResetMap()
        {
            _mapLifecycle.Reset();
        }

        private void AddDefaultBuilders()
        {
            // initialize node builders
            foreach (var builder in _builders)
                RegisterBuilder(builder);

            if (_builderRegistry.Count == 0)
                Message.Send("SceneManager", MessageLevel.WARNING, "no node builder registered");
            

        }


        private bool InitializeInternal()
        {
            // Initialize streamer APIs
            if (!GizmoSDK.Gizmo3D.Platform.Initialize())
                return false;

            // Initialize formats
            DbManager.Initialize();

            GizmoSDK.GizmoBase.Message.Send("SceneManager", MessageLevel.DEBUG, "Initialize Graph Streaming");

            // Add builder for registered types
            AddDefaultBuilders();

            foreach (var builder in _builderRegistry)
            {
                builder.SetTextureManager(_textureManager);
                builder.SetMaterialManager(_materialManager);
            }

            _nodeHandlePool.Initialize(_poolPolicyRegistry);

            if (!_nodeHandlePool.HasPool(PoolObjectFeature.StaticMesh))
            {
                _settings.Options |= SceneManagerOptions.DisableInstancing;
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "disabling instancing, no builder for StaticMesh feature");
            }

            _traversalConfiguration.Update(Settings);
            _dynamicNodeLoadCoordinator.Subscribe();
            _sceneTraverser.SetActionReceiver(_dynamicNodeLoadCoordinator.ActionReceiver);

            
            NodeLock.WaitLockEdit();

            try // We are now locked in edit
            {
                _nativeScene.Initialize();

                _nativeTraverseAction = new CullTraverseAction();
            }
            finally
            {

                NodeLock.UnLock();
            }

            // Set up dynamic loading
            DynamicLoader.UsePreCache(true);                    // Enable use of mipmap creation on dynamic loading
            DynamicLoaderManager.SetNumberOfActiveLoaders(Settings.DynamicLoaders);   // Lets start with 4 parallell threads
            DynamicLoaderManager.StartManager();

            // Start coroutines for asset loading
            StartCoroutine(_externalAssetLoader.Process());

            return true;
        }

        public bool Uninitialize()
        {
            if (!_initialized)
                   return false;

            // Stop manager
            DynamicLoaderManager.StopManager();

            ResetMap();

            NodeLock.WaitLockEdit();

            try // We are now locked in edit
            {

                _nativeScene.Dispose();

            }
            finally
            {
                NodeLock.UnLock();
            }

            _dynamicNodeLoadCoordinator.Unsubscribe();
            _sceneTraverser.SetActionReceiver(null);

            _initialized = false;

            return true;
        }

        public bool Init(bool loadMap = true)
        {
            if (_initialized)
                return true;

            if (!InitializeInternal())
                return false;

            _initialized = true;

            if (loadMap && !LoadMap())
            {
                Uninitialize();
                return false;
            }

            return true;
        }


        private void Start()
        {
            Init();
        }

        private void OnEnable()
        {
            if (_resumeOnEnable)
                Init();
        }

        private void OnApplicationQuit()
        {
            Uninitialize();
        }

        private void OnDisable()
        {
            _resumeOnEnable = _initialized;
            if (_initialized)
                Uninitialize();
        }
               
        // Update is called once per frame
        private void Update()
        {
            if (Settings.Options.HasFlag(SceneManagerOptions.RenderInUpdate))
                Render();
        }

        public void Render()
        {
            // Check if global world camera is present -----------------------
            if (SceneManagerCamera == null)
                return;

            var traversed = false;
            using (ProfilerMarkerRender.Auto())
            {
                var frame = new StreamingFrameContext(
                    SceneManagerCamera,
                    SceneManagerCamera.Camera,
                    _nativeScene.Camera,
                    _nativeScene.Context,
                    _nativeTraverseAction,
                    Settings,
                    NotifyPreTraverse,
                    NotifyCameraUpdated);
                traversed = _streamingPipeline.ProcessFrame(frame);
            }

            if (traversed)
            {
                SceneManagerCamera.PostTraverse(false);
                OnPostTraverse?.Invoke(false);
            }
        }

        private void NotifyPreTraverse(bool locked) =>
            OnPreTraverse?.Invoke(locked);

        private void NotifyCameraUpdated(double renderTime) =>
            OnUpdateCamera?.Invoke(renderTime);

    }
}



