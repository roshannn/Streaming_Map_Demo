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
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Traversal.Events;
using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;
using Saab.Unity.Extensions;
using Saab.Utility.Unity.NodeUtils;
using Saab.Utility.GfxCaps;

// Fix unity conflicts
using unTransform = UnityEngine.Transform;

// System
using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine.Networking;
using System.Linq;

using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;

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

    /// <summary>
    /// Flags used during traversal to keep track of state
    /// </summary>
    [Flags]
    public enum TraversalState
    {
        None,
        /// <summary>
        /// Set when traversing an asset subgraph i.e. /Resources
        /// </summary>
        Asset = 0x01,

        /// <summary>
        /// Set when traversing a gzRefNode subgraph
        /// </summary>
        AssetInstance = 0x02,
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
    public class SceneManager : MonoBehaviour
    {
        public SceneManagerSettings Settings = SceneManagerSettings.Default;
        public ISceneManagerCamera  SceneManagerCamera;
        public string               MapUrl;
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
        private static readonly ProfilerMarker _profilerFree = new ProfilerMarker(ProfilerCategory.Render, "SM-Free");

        private bool _initialized;

        private readonly NodeUpdateRegistry _nodeUpdateRegistry =
            new NodeUpdateRegistry();
        private readonly ExternalAssetLoader _externalAssetLoader =
            new ExternalAssetLoader();
        private NodeHandleFactory _nodeHandleFactory;

        private readonly GeometryBuilderRegistry _builderRegistry =
            new GeometryBuilderRegistry();
        private readonly NodeBuildScheduler _buildScheduler =
            new NodeBuildScheduler();
        private GeometryNodeOperations _geometryOperations;

        // Used by builders to share and manage texture resources
        private readonly TextureManager _textureManager = new TextureManager();

        // Used by builders to share and manage Material resources
        private readonly MaterialManager _materialManager = new MaterialManager();

        private SceneTraverser _sceneTraverser;
        private DynamicNodeLoadCoordinator _dynamicNodeLoads;
        private NodeEvents _nodeEvents;

        private NodeEvents NodeEvents
        {
            get
            {
                if (_nodeEvents == null)
                    _nodeEvents = GetComponent<NodeEvents>();

                return _nodeEvents;
            }
        }

        private void Awake()
        {
            _nodeEvents = GetComponent<NodeEvents>();

            if (_nodeEvents == null)
                _nodeEvents = gameObject.AddComponent<NodeEvents>();
        }

        private SceneTraverser SceneTraverser
        {
            get
            {
                if (_sceneTraverser == null)
                {
                    if (_nodeHandleFactory == null)
                        _nodeHandleFactory = new NodeHandleFactory(Allocate);

                    if (_geometryOperations == null)
                    {
                        _geometryOperations = new GeometryNodeOperations(
                            _builderRegistry,
                            _buildScheduler,
                            _nodeHandleFactory,
                            NodeEvents);
                    }

                    _sceneTraverser =
                        new SceneTraverser(
                            this,
                            NodeEvents,
                            _nodeUpdateRegistry,
                            _externalAssetLoader,
                            _nodeHandleFactory,
                            _geometryOperations);
                }

                return _sceneTraverser;
            }
        }

        // Pools of pre allocated and recycled node objects, used to avoid runtime allocations and instead recycle game objects
        private readonly Stack<NodeHandle>[] _free = new Stack<NodeHandle>[byte.MaxValue];

        // Prefab when allocating node handles for specific pools
        private readonly NodeHandle[] _poolPrefabs = new NodeHandle[byte.MaxValue];

        // Stores objects that have been unloaded but not yet freed, objects will eventually be returned to the free list,
        // this is to reduce the time spent freeing nodes in a single frame
        private readonly Stack<unTransform> _pendingFrees = new Stack<unTransform>();

        // Used during pre allocation to spread allocations evenly across pools
        private readonly Queue<byte> _preAllocationRoundRobinQueue = new Queue<byte>();

        public void AddBuilder(INodeBuilder builder)
        {
            if (_initialized)
                throw new InvalidOperationException("builders must be registered before init");
            
            _builderRegistry.Add(builder);
        }

        public void RemoveBuilder(INodeBuilder builder)
        {
            _builderRegistry.Remove(builder);
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
                    GameObject scene = SceneTraverser.Begin(MapControl.SystemMap.CurrentMap);

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

                _dynamicNodeLoads?.Reset();

                // allow builders to perform custom clean up
                _builderRegistry.Reset();
                _geometryOperations?.Reset();

                if (_root)
                {
                    UnloadHierarchy(_root.transform);
                    Free(_root.transform);
                    FreeFromPendingQueue(int.MaxValue);
                    _root = null;
                }

                _native_scene?.RemoveAllNodes();

                _textureManager.Clear();
                _materialManager.Clear();

                // clear all pending asset loads
                SceneTraverser.AssetPolicy.ClearDeferred();

                // clear any pending builds
                _buildScheduler.Clear();
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
                _builderRegistry.Add(builder);

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

            // init object pooling
            _free[(byte)PoolObjectFeature.None] = new Stack<NodeHandle>(65000);
            
            // init allocator prefab for logical objects
            _poolPrefabs[0] = CreateAllocatorPrefabForBuilder(null);

            foreach (var builder in _builderRegistry)
            {
                var idx = (byte)builder.Feature;
                if (_free[idx] == null)
                {
                    _free[idx] = new Stack<NodeHandle>(65000);
                    _poolPrefabs[idx] = CreateAllocatorPrefabForBuilder(builder);
                }

                builder.SetTextureManager(_textureManager);
                builder.SetMaterialManager(_materialManager);
            }

            var pools = _free.Where(p => p != null).ToArray();
            foreach (byte poolId in pools.Select(p => (byte)Array.IndexOf(_free, p)))
                _preAllocationRoundRobinQueue.Enqueue(poolId);

            if (_poolPrefabs[(int)PoolObjectFeature.StaticMesh] == null)
            {
                Settings.Options |= SceneManagerOptions.DisableInstancing;
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "disabling instancing, no builder for StaticMesh feature");
            }

        


            _dynamicNodeLoads = new DynamicNodeLoadCoordinator(
                SceneTraverser.Begin,
                UnloadHierarchy,
                Free);
            _dynamicNodeLoads.Subscribe();
            SceneTraverser.SetActionReceiver(_dynamicNodeLoads.ActionReceiver);

            
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

        private NodeHandle CreateAllocatorPrefabForBuilder(INodeBuilder builder)
        {
            var feature = builder != null ? builder.Feature : PoolObjectFeature.None;

            var prefab = new GameObject();
            prefab.SetActive(false);
#if UNITY_EDITOR
            prefab.hideFlags = HideFlags.HideInHierarchy;
#endif
            var nh = prefab.AddComponent<NodeHandle>();
            nh.featureKey = feature;
            builder?.InitPoolObject(prefab);

            return nh;
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

            _dynamicNodeLoads?.Dispose();
            _dynamicNodeLoads = null;
            SceneTraverser.SetActionReceiver(null);

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

            // Process changes of the scenegraph
            _profilerMarkerTraverse.Begin();
            _dynamicNodeLoads.ProcessLoads();
            _profilerMarkerTraverse.End();
        }

        private void ProcessPendingUpdatesPostTraversal()
        {
            // We must be called in edit lock

            #region Activate/Deactivate GameObjects based on scenegraph -----------------------------------------------------

            _dynamicNodeLoads.ProcessActivations();

            #endregion

            #region Update slow loading assets ------------------------------------------------------------------------------

            // free up to a maximum number of nodes
            FreeFromPendingQueue(1000);

            // make sure we have available nodes in our pools
            PreAllocateNodeHandle(10000, TimeSpan.FromMilliseconds(1));


            var remainingBuildTime = TimeSpan.FromSeconds(Settings.MaxBuildTime) - _renderTimer.Elapsed;
            if (remainingBuildTime < TimeSpan.FromSeconds(Settings.MinBuildTime))
                remainingBuildTime = TimeSpan.FromSeconds(Settings.MinBuildTime);

            _buildScheduler.Process(remainingBuildTime);

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


            if (_dynamicNodeLoads.HasPendingLoads) // Check if we got a mismatch in updates
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

        private NodeHandle Allocate(PoolObjectFeature featureKey, Node node)
        {
            var idx = (byte)featureKey;
            var pool = _free[idx];               

            if (pool.Count == 0)
                FreeFromPendingQueue(100);
            
            if (pool.Count > 0)
            {
                var res = pool.Pop();
                res.node = node;

                // init
                res.gameObject.SetActive(true);             // <-- stupid slow
#if UNITY_EDITOR
                res.gameObject.hideFlags = HideFlags.None;
#endif

                return res;
            }

            var nh = Instantiate(_poolPrefabs[idx]);
            nh.node = node;
            nh.gameObject.SetActive(true);
            return nh;
        }

        private void Free(unTransform transform)
        {
            transform.parent = null;
            transform.gameObject.SetActive(false);
#if UNITY_EDITOR
            transform.hideFlags = HideFlags.HideInHierarchy;
#endif
            
            _pendingFrees.Push(transform);
        }

        private void UnloadHierarchy(unTransform transform)
        {
            if (transform.TryGetComponent<NodeHandle>(out var nodeHandle))
            {
                // remove from update list
                if (nodeHandle.inNodeUpdateList)
                    _nodeUpdateRegistry.Unregister(nodeHandle);

                // remove from registry
                if (nodeHandle.inNodeUtilsRegistry)
                    NodeUtils.RemoveGameObjectReferenceUnsafe(nodeHandle.node.GetNativeReference(), transform.gameObject);

                // invalidate any pending builds for this node handle
                nodeHandle.version++;
            }
            
            // recurse down the hierarchy
            for (var i = 0; i < transform.childCount; ++i)
                UnloadHierarchy(transform.GetChild(i));
        }

        private void FreeFromPendingQueue(int count)
        {
            _profilerFree.Begin();
            while (_pendingFrees.Count > 0 && count > 0)
            {
                var free = _pendingFrees.Pop();
        
                // orphan all children and put them on the free frontier
                for (var i = free.childCount - 1; i >= 0; --i)
                    Free(free.GetChild(i));
        
                FreeInternal(free);
        
                --count;
            }
            _profilerFree.End();
        }

        private void PreAllocateNodeHandle(int count, TimeSpan timeBudget)
        {
            if (_preAllocationRoundRobinQueue.Count == 0)
                return;

            var timer = System.Diagnostics.Stopwatch.StartNew();

            var fullyAllocatedPools = 0;

            while (timer.Elapsed < timeBudget && fullyAllocatedPools < _preAllocationRoundRobinQueue.Count)
            {
                var poolId = _preAllocationRoundRobinQueue.Dequeue();
                _preAllocationRoundRobinQueue.Enqueue(poolId);
                
                var pool = _free[poolId];

                // do in batches of 100
                var remaining = count - pool.Count;
                if (remaining > 100)
                    remaining = 100;

                if (pool.Count < count)
                    AllocateNodeHandleForPool(poolId, remaining);
                else
                    fullyAllocatedPools++;
            }
        }

        private void AllocateNodeHandleForPool(byte poolId, int count)
        {
            for (var i = 0; i < count; ++i)
            {
                var nh = Instantiate(_poolPrefabs[poolId]);
#if UNITY_EDITOR
                nh.gameObject.hideFlags = HideFlags.HideInHierarchy;
#endif
                _free[poolId].Push(nh);
            }
        }
        

        private void FreeInternal(unTransform transform)
        {
            if (transform.TryGetComponent<NodeHandle>(out var nodeHandle))
                FreeHandle(nodeHandle);
            else
                Destroy(transform.gameObject);
        }

        private void FreeHandle(NodeHandle nodeHandle)
        {
            // get pool managing this type of node
            var pool = _free[(byte)nodeHandle.featureKey];

            // return the handle to the pool
            pool.Push(nodeHandle);

            var go = nodeHandle.gameObject;
            
            var tr = go.transform;
            //tr.parent = null;
            tr.localPosition = Vector3.zero;
            tr.localRotation = UnityEngine.Quaternion.identity;
            tr.localScale = Vector3.one;

            var node = nodeHandle.node;

            if (nodeHandle.builder != null)
            {
                bool sharedNode = nodeHandle.stateFlags.HasFlag(NodeStateFlags.AssetInstance);
                nodeHandle.builder.BuiltObjectReturnedToPool(go, sharedNode);
            }

            if (node is Geometry)
            {
                switch (nodeHandle.featureKey)
                {
                    case PoolObjectFeature.Terrain:
                        NodeEvents.NotifyTerrainRemoved(go);
                        break;
                    case PoolObjectFeature.StaticMesh:
                        NodeEvents.NotifyGeometryRemoved(go);
                        break;
                    default:
                        break;
                }
            }

            nodeHandle.Recycle(_textureManager);

            NodeEvents.NotifyEnteredPool(go);
        }
    }

    [Flags]
    public enum PoolObjectFeature : byte
    {
        //
        None = 0,

        // Terrain
        Terrain = 1 << 0,

        //
        StaticMesh = 1 << 1,

        //
        Crossboard = 1 << 2,
    }


}



