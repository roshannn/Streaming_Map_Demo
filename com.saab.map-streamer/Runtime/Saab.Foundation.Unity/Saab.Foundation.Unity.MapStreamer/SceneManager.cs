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
        public SceneManagerSettings Settings = SceneManagerSettings.Default;
        public ISceneManagerCamera  SceneManagerCamera { get; private set; }
        public string               MapUrl { get; private set; }
        public NodeBuilderBase[] Builders;

        // Events ----------------------------------------------------------

        public delegate void EventHandler_OnNode(Node node);
        public delegate void EventHandler_OnUpdateCamera(double renderTime);
        public delegate void EventHandler_OnMapLoadError(ref string url,string errorString,SerializeAdapter.AdapterError errorType,ref bool retry);

        public delegate void EventHandler_Traverse(bool locked);    // Pre and Post traversal in locked or unlocked mode (edit)

        public event EventHandler_Traverse          OnPreTraverse;  // Called before SceneManagerCamera is updated
        public event EventHandler_Traverse          OnPostTraverse; // Called after SceneManagerCamera is updated
        public event EventHandler_OnNode            OnMapChanged;
        public event EventHandler_OnMapLoadError    OnMapLoadError;
        public event EventHandler_OnUpdateCamera    OnUpdateCamera; // Called after SceneManagerCamera is updated

        #region ------------- Privates ----------------

        private CullTraverseAction _native_traverse_action;
        private GameObject _root;

        private readonly string ID = "Saab.Foundation.Unity.MapStreamer.SceneManager";

        //#pragma warning disable 414
        //private UnityPluginInitializer _plugin_initializer;
        //#pragma warning restore 414

        #endregion


        private static readonly ProfilerMarker _profilerMarkerRender = new ProfilerMarker(ProfilerCategory.Render, "SM-Render");
        private bool _initialized;

        private INodeUpdateRegistry _nodeUpdateRegistry;
        private IExternalAssetQueue _externalAssetLoader;
        private GeometryBuilderRegistry _builderRegistry;
        private PooledNodeObjectPolicyRegistry _poolPolicyRegistry;
        private NodeBuildCoordinator _buildCoordinator;
        private IGeometryNodeOperations _geometryOperations;

        // Used by builders to share and manage texture resources
        private TextureManager _textureManager;

        // Used by builders to share and manage Material resources
        private MaterialManager _materialManager;

        private SceneTraverser _sceneTraverser;
        private DynamicNodeLoadCoordinator _dynamicNodeLoadCoordinator;
        private NodeHandlePool _nodeHandlePool;
        private NodeHierarchyUnloader _hierarchyUnloader;
        private ITraversalConfiguration _traversalConfiguration;
        private StreamingPipeline _streamingPipeline;
        private NativeSceneResources _nativeScene;

        [Inject]
        private void Construct(
            INodeUpdateRegistry nodeUpdateRegistry,
            IExternalAssetQueue externalAssetLoader,
            GeometryBuilderRegistry builderRegistry,
            PooledNodeObjectPolicyRegistry poolPolicyRegistry,
            NodeBuildCoordinator buildCoordinator,
            IGeometryNodeOperations geometryOperations,
            TextureManager textureManager,
            MaterialManager materialManager,
            SceneTraverser sceneTraverser,
            DynamicNodeLoadCoordinator dynamicNodeLoads,
            NodeHandlePool nodeHandlePool,
            NodeHierarchyUnloader hierarchyUnloader,
            ITraversalConfiguration traversalConfiguration,
            StreamingPipeline streamingPipeline,
            NativeSceneResources nativeScene,
            MapConfig mapConfig,
            ISceneManagerCamera sceneManagerCamera)
        {
            _nodeUpdateRegistry = nodeUpdateRegistry;
            _externalAssetLoader = externalAssetLoader;
            _builderRegistry = builderRegistry;
            _poolPolicyRegistry = poolPolicyRegistry;
            _buildCoordinator = buildCoordinator;
            _geometryOperations = geometryOperations;
            _textureManager = textureManager;
            _materialManager = materialManager;
            _sceneTraverser = sceneTraverser;
            _dynamicNodeLoadCoordinator = dynamicNodeLoads;
            _nodeHandlePool = nodeHandlePool;
            _hierarchyUnloader = hierarchyUnloader;
            _traversalConfiguration = traversalConfiguration;
            _streamingPipeline = streamingPipeline;
            _nativeScene = nativeScene;
            SceneManagerCamera = sceneManagerCamera;
            MapUrl = mapConfig.MapUrl;
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

        // The LoadMap function takes an URL and loads the map into GizmoSDK native db
        public bool LoadMap(string mapURL)
        {
            NodeLock.WaitLockEdit();      // We assume we do all editing from main thread and to allow render we assume we edit in edit mode

            try // We are now locked in edit
            {

                if (!ResetMap())
                    return false;

                Node node = null;

                while (true)
                {
                    if (string.IsNullOrEmpty(mapURL))
                        break;

                    string errorString = "";
                    SerializeAdapter.AdapterError errorType = SerializeAdapter.AdapterError.NO_ERROR;
                    bool retry = false;
                    node = DbManager.LoadDB(mapURL, ref errorString, ref errorType);

                    if (node == null || !node.IsValid())
                    {
                        Message.Send(ID, MessageLevel.WARNING, $"Failed to load map {mapURL}");

                        OnMapLoadError?.Invoke(ref mapURL, errorString, errorType, ref retry);


                        if (retry)
                        {
                            continue;
                        }

                        return false;
                    }

                    break;
                }

                MapUrl = mapURL;

                MapControl.SystemMap.NodeURL = mapURL;
                MapControl.SystemMap.CurrentMap = node;

                

                if (node != null)
                {
                    _nativeScene.AddNode(MapControl.SystemMap.CurrentMap);

                    _root = new GameObject("root");
                    GameObject scene = _sceneTraverser.Begin(MapControl.SystemMap.CurrentMap);

                    if (scene != null)
                        scene.transform.SetParent(_root.transform, false);

                    // As GizmoSDK has a flipped Z axis going out of the screen we need a top transform to flip Z
                    _root.transform.localScale = new Vector3(1, 1, -1);
                }
                
                

                //// Add example object under ROI --------------------------------------------------------------

                //MapPos mapPos;

                //GetMapPosition(new LatPos(1.0084718541, 0.24984267815, 300), out mapPos, GroundClampType.GROUND, true);

                //_test = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                //_test.transform.parent = FindFirstGameObjectTransform(mapPos.roiNode);
                //_test.transform.localPosition = mapPos.position.ToVector3();
                //_test.transform.localScale = new Vector3(10, 10, 10);

                if (SceneManagerCamera != null)
                    SceneManagerCamera.MapChanged();

                OnMapChanged?.Invoke(node);
            }
            finally
            {
                NodeLock.UnLock();
            }

            return true;
        }

        public bool ResetMap()
        {
            //MapUrl = null;

            NodeLock.WaitLockEdit();

            try // We are now locked in edit
            {

                _dynamicNodeLoadCoordinator?.Reset();

                // allow builders to perform custom clean up
                _builderRegistry.Reset();
                _geometryOperations?.Reset();

                if (_root)
                {
                    _hierarchyUnloader.Unload(_root.transform);
                    _nodeHandlePool.QueueFree(_root.transform);
                    _nodeHandlePool.ProcessPending(int.MaxValue);
                    _root = null;
                }

                _nativeScene?.ClearScene();

                _textureManager.Clear();
                _materialManager.Clear();

                // clear all pending asset loads
                _sceneTraverser.AssetPolicy.ClearDeferred();

                // clear any pending builds
                _buildCoordinator.Clear();
                _nodeUpdateRegistry.Clear();
                _externalAssetLoader.Clear();

                MapControl.SystemMap.Reset();
            }
            finally
            {
                NodeLock.UnLock();
            }

            return true;
        }

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
            _dynamicNodeLoadCoordinator.Subscribe();
            _sceneTraverser.SetActionReceiver(_dynamicNodeLoadCoordinator.ActionReceiver);

            
            NodeLock.WaitLockEdit();

            try // We are now locked in edit
            {
                _nativeScene.Initialize();

                // Default travrser
                _native_traverse_action = new CullTraverseAction();

                // _native_traverse_action.SetOmniTraverser(true);  // To skip camera cull and use LOD in omni directions


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
            if (loadMap && !LoadMap(MapUrl))
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
                    _nativeScene.Camera,
                    _nativeScene.Context,
                    _native_traverse_action,
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



