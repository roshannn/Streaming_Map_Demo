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

// Gizmo Managed classes
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

// Fix some conflicts between unity and Gizmo namespaces
using gzCamera = GizmoSDK.Gizmo3D.Camera;
using gzTexture = GizmoSDK.Gizmo3D.Texture;


// Map utility
using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.DynamicLoading;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
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
        Matrix4x4 NativeWorldToLocalMatrix { get; }
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
        public SceneManagerSettings Settings = SceneManagerSettings.Default;
        public ISceneManagerCamera  SceneManagerCamera;
        public NodeBuilderBase[] Builders;

        // Events ----------------------------------------------------------

        public delegate void EventHandler_OnUpdateCamera(double renderTime);

        public delegate void EventHandler_Traverse(bool locked);    // Pre and Post traversal in locked or unlocked mode (edit)

        public event EventHandler_Traverse          OnPreTraverse;  // Called before SceneManagerCamera is updated
        public event EventHandler_Traverse          OnPostTraverse; // Called after SceneManagerCamera is updated
        public event EventHandler_OnUpdateCamera    OnUpdateCamera; // Called after SceneManagerCamera is updated

        #region ------------- Privates ----------------

        private readonly string ID = "Saab.Foundation.Unity.MapStreamer.SceneManager";

        //#pragma warning disable 414
        //private UnityPluginInitializer _plugin_initializer;
        //#pragma warning restore 414

        #endregion


        private static readonly ProfilerMarker _profilerMarkerRender = new ProfilerMarker(ProfilerCategory.Render, "SM-Render");
        private bool _initialized;

        private IExternalAssetQueue _externalAssetLoader;
        private GeometryBuilderRegistry _builderRegistry;
        private PooledNodeObjectPolicyRegistry _poolPolicyRegistry;

        // Used by builders to share and manage texture resources
        private TextureManager _textureManager;

        // Used by builders to share and manage Material resources
        private MaterialManager _materialManager;

        private NodeHandlePool _nodeHandlePool;
        private ITraversalConfiguration _traversalConfiguration;
        private StreamingPipeline _streamingPipeline;
        private MapSession _mapSession;

        [Inject]
        private void Construct(
            IExternalAssetQueue externalAssetLoader,
            GeometryBuilderRegistry builderRegistry,
            PooledNodeObjectPolicyRegistry poolPolicyRegistry,
            TextureManager textureManager,
            MaterialManager materialManager,
            NodeHandlePool nodeHandlePool,
            ITraversalConfiguration traversalConfiguration,
            StreamingPipeline streamingPipeline,
            MapSession mapSession)
        {
            _externalAssetLoader = externalAssetLoader;
            _builderRegistry = builderRegistry;
            _poolPolicyRegistry = poolPolicyRegistry;
            _textureManager = textureManager;
            _materialManager = materialManager;
            _nodeHandlePool = nodeHandlePool;
            _traversalConfiguration = traversalConfiguration;
            _streamingPipeline = streamingPipeline;
            _mapSession = mapSession;
            _mapSession.MapChanged += HandleMapChanged;
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

        private void HandleMapChanged(Node node) =>
            SceneManagerCamera?.MapChanged();

        private void AddDefaultBuilders()
        {
            // initialize node builders
            foreach (var builder in Builders)
                RegisterBuilder(builder);

            if (_builderRegistry.Count == 0)
                Message.Send("SceneManager", MessageLevel.WARNING, "no node builder registered");
            

            // ************* [Deprecated] *************
            //if (GfxCaps.CurrentCaps.HasFlag(Capability.UseTreeCrossboards))
            //AddBuilder(new CrossboardNodeBuilder(Settings.CrossboardShader, Settings.ComputeShader));
        }


        public bool InitializeInternal()        
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
                Settings.Options |= SceneManagerOptions.DisableInstancing;
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "disabling instancing, no builder for StaticMesh feature");
            }

            _traversalConfiguration.Update(Settings);
            _mapSession.Initialize();

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

            _mapSession.Dispose();

            // Dont do this as Unity wants to keep modules loaded
            //// Drop platform streamer
            //GizmoSDK.Gizmo3D.Platform.Uninitialize();

            _initialized = false;

            return true;
        }

        public bool Init(bool loadMap = true)
        {
            if (_initialized)
                return true;

            _initialized = true;
           
            // Initialize this manager
            if (!InitializeInternal())
                return false;

            // Load the map
            if (loadMap && !_mapSession.Load())
                return false;

            return true;
        }


        private void Start()
        {
            Init();
        }

        private void OnEnable()
        {
            // important that we do not run Init() from OnEable() since that will run during AddComponent(),
            // and in BTA we need to control when the map is loaded.
            if (!_initialized)
                return;

            Init();
        }

        private void OnApplicationQuit()
        {
            Uninitialize();
        }

        private void OnDisable()
        {
            // We add Unitialize and shut down threads here as this routine gets called by an edit in code
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

            using (_profilerMarkerRender.Auto())
            {
                var frame = new StreamingFrameContext(
                    SceneManagerCamera,
                    SceneManagerCamera.Camera,
                    _mapSession.NativeCamera,
                    _mapSession.NativeContext,
                    _mapSession.TraverseAction,
                    Settings,
                    NotifyPreTraverse,
                    NotifyCameraUpdated);
                _streamingPipeline.ProcessFrame(frame);
            }
            
            // -------------------------------------------------------------
            SceneManagerCamera.PostTraverse(false);
            OnPostTraverse?.Invoke(false);
        }

        private void NotifyPreTraverse(bool locked) =>
            OnPreTraverse?.Invoke(locked);

        private void NotifyCameraUpdated(double renderTime) =>
            OnUpdateCamera?.Invoke(renderTime);

    }
}



