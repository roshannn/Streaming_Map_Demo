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
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;
using Saab.Unity.Extensions;
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
        public ISceneManagerCamera  SceneManagerCamera;
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

        private Scene _native_scene;
        private gzCamera _native_camera;
        private Context _native_context;
        private CullTraverseAction _native_traverse_action;
        private GameObject _root;

        private readonly string ID = "Saab.Foundation.Unity.MapStreamer.SceneManager";

        //#pragma warning disable 414
        //private UnityPluginInitializer _plugin_initializer;
        //#pragma warning restore 414

        #endregion


        private static readonly ProfilerMarker _profilerMarkerRender = new ProfilerMarker(ProfilerCategory.Render, "SM-Render");
        private static readonly ProfilerMarker _profilerMarkerCull = new ProfilerMarker(ProfilerCategory.Render, "SM-Cull");
        private static readonly ProfilerMarker _profilerMarkerTraverse = new ProfilerMarker(ProfilerCategory.Render, "SM-Traverse");
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
            MapConfig mapConfig)
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
                    _native_scene.AddNode(MapControl.SystemMap.CurrentMap);
#if DEBUG
                    _native_scene.Debug();
#endif

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

                _native_scene?.RemoveAllNodes();

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
                // Camera setup
                _native_camera = new PerspCamera("Test");
                _native_camera.RoiPosition = true;
                MapControl.SystemMap.Camera = _native_camera;

                // Top scene
                _native_scene = new Scene("Scene");
                _native_camera.Scene = _native_scene;

                // Top context
                _native_context = new Context();

#if DEBUG_CAMERA

                // If we want to visualize debug 3D
                _native_camera.Debug(_native_context);      // Enable to debug view

#endif // DEBUG_CAMERA

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

                _native_camera.Debug(_native_context, false);
                _native_camera.Dispose();
                _native_camera = null;

                _native_context.Dispose();
                _native_context = null;

                _native_scene.Dispose();
                _native_scene = null;

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
               
        private void ProcessPendingUpdatesPreTraversal()
        {
            // We must be called in edit lock
            _traversalConfiguration.Update(Settings);

            // Process changes of the scenegraph
            _profilerMarkerTraverse.Begin();
            _dynamicNodeLoadCoordinator.ProcessLoads();
            _profilerMarkerTraverse.End();
        }

        private void ProcessPendingUpdatesPostTraversal()
        {
            // We must be called in edit lock

            #region Activate/Deactivate GameObjects based on scenegraph -----------------------------------------------------

            _dynamicNodeLoadCoordinator.ProcessActivations();

            #endregion

            #region Update slow loading assets ------------------------------------------------------------------------------

            // free up to a maximum number of nodes
            _nodeHandlePool.ProcessPending(1000);

            // make sure we have available nodes in our pools
            _nodeHandlePool.PreAllocate(10000, TimeSpan.FromMilliseconds(1));


            var remainingBuildTime = TimeSpan.FromSeconds(Settings.MaxBuildTime) - _renderTimer.Elapsed;
            if (remainingBuildTime < TimeSpan.FromSeconds(Settings.MinBuildTime))
                remainingBuildTime = TimeSpan.FromSeconds(Settings.MinBuildTime);

            _buildCoordinator.Process(remainingBuildTime);

            #endregion
        }

        private void UpdateNodeInternals()
        {
            // Only called if SceneManagerCamera is not null
            _nodeUpdateRegistry.UpdateNodes();
        }

        // Update is called once per frame
        private void Update()
        {
            if (Settings.Options.HasFlag(SceneManagerOptions.RenderInUpdate))
                Render();
        }

        private readonly System.Diagnostics.Stopwatch _renderTimer = new System.Diagnostics.Stopwatch();

        public void Render()
        {
            _renderTimer.Restart();

            // Check if global world camera is present -----------------------
            if (SceneManagerCamera == null)
                return;

            _profilerMarkerRender.Begin();

            RenderInternal();

            _profilerMarkerRender.End();
            
            // -------------------------------------------------------------
            SceneManagerCamera.PostTraverse(false);
            OnPostTraverse?.Invoke(false);
        }

        private void RenderInternal()
        {
            // Check if local unity camera is present ------------------------
            var unityCamera = SceneManagerCamera.Camera;
            if (unityCamera == null)
                return;

            // Check if local native camera is present ------------------------
            if (_native_camera == null)
                return;

            // Lets try to build a scenegraph from pending changes from previous pass
            if (!NodeLock.TryLockEdit(30))      // 30 msek allow latency of other pending editor
            {
                Message.Send(ID, MessageLevel.DEBUG, "Lock contention detected! NodeLock::TryLockEdit() FRAME LOST");

                // We failed to refresh scene in reasonable time but we still need to issue updates;
                SceneManagerCamera.PreTraverse(false);
                OnPreTraverse?.Invoke(false);
                return;
            }

            // Signal the world camera we are in pre traverse locked
            SceneManagerCamera.PreTraverse(true);

            // Signal the SM we are in pre traverse locked
            OnPreTraverse?.Invoke(true);

            // Builds a scenegraph from changes from previous frame
            ProcessPendingUpdatesPreTraversal();

            if (!NodeLock.ChangeToRenderLock())
            {
                NodeLock.UnLock();
                Message.Send(ID, MessageLevel.DEBUG, "Failed to change into RenderLock");
            }

            if (!NodeLock.IsLockedRender())
                return;


            if (_dynamicNodeLoadCoordinator.HasPendingLoads) // Check if we got a mismatch in updates
            {
                NodeLock.UnLock(); // Unlock render
                Message.Send(ID, MessageLevel.FATAL, "Mismatch in virtual context (loaded/unloaded data)");
                return;
            }

            // We are now locked in Render
            _profilerMarkerCull.Begin();
            RenderInternal(unityCamera);
            _profilerMarkerCull.End();

            if (!NodeLock.ChangeToEditLock())
            {
                NodeLock.UnLock();
                Message.Send(ID, MessageLevel.DEBUG, "Failed to change into EditLock");
            }

            if (!NodeLock.IsLockedByMe())
                return;

            // Builds a scenegraph from changes from previous frame
            ProcessPendingUpdatesPostTraversal();

            NodeLock.UnLock();

            // Unlocked updates
            UpdateNodeInternals();
        }

        private void RenderInternal(UnityEngine.Camera UnityCamera)
        {
            // We are now locked in Render

            // Setup LOD

            // lod bias
            var lodFactor = SceneManagerCamera.LodFactor;
            Lod.SetLODFactor(_native_context, lodFactor);
            MapControl.SystemMap.LodFactor = lodFactor;

            // Transfer camera parameters

            PerspCamera perspCamera = _native_camera as PerspCamera;

            // Right now we use system time as render time but this can be controlled externally in the future
            var renderTime = GizmoSDK.GizmoBase.Time.SystemSeconds;

            // Syncronized update. You should use the rendertime
            renderTime = SceneManagerCamera.UpdateCamera(renderTime);
            OnUpdateCamera?.Invoke(renderTime);

            // Use this time in render
            _native_context.CurrentRenderTime = renderTime;

            if (perspCamera != null)
            {
                perspCamera.VerticalFOV = UnityCamera.fieldOfView;
                perspCamera.HorizontalFOV = 2 * Mathf.Atan(Mathf.Tan(UnityCamera.fieldOfView * Mathf.Deg2Rad / 2) * UnityCamera.aspect) * Mathf.Rad2Deg; ;
                perspCamera.NearClipPlane = UnityCamera.nearClipPlane;
                perspCamera.FarClipPlane = UnityCamera.farClipPlane;
            }

            Matrix4x4 unity_camera_transform = UnityCamera.transform.worldToLocalMatrix;

            _native_camera.Transform = unity_camera_transform.ToZFlippedMatrix4();

            _native_camera.Position = SceneManagerCamera.GlobalPosition;

            _native_camera.Render(_native_context, 1000, 1000, 1000, _native_traverse_action);

#if DEBUG_CAMERA
                     _native_camera.DebugRefresh();
#endif
        }

    }
}



