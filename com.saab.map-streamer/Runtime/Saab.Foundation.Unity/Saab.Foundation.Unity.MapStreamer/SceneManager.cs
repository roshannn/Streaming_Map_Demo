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

// Unity Managed classes
using UnityEngine;

// Gizmo Managed classes
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

// Map utility
using Saab.Foundation.Unity.MapStreamer.MapSessions;
using Saab.Foundation.Unity.MapStreamer.Native;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Streaming.Pipeline;
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

    [RequireComponent(typeof(NodeEvents))]
    [RequireComponent(typeof(MapStreamerLifetimeScope))]
    public class SceneManager : MonoBehaviour
    {
        public ISceneManagerCamera  SceneManagerCamera;
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
        private StreamingPipeline _streamingPipeline;
        private MapSession _mapSession;
        private NativeSceneSession _nativeSceneSession;

        [Inject]
        private void Construct(
            IExternalAssetQueue externalAssetLoader,
            GeometryBuilderRegistry builderRegistry,
            PooledNodeObjectPolicyRegistry poolPolicyRegistry,
            TextureManager textureManager,
            MaterialManager materialManager,
            NodeHandlePool nodeHandlePool,
            StreamingPipeline streamingPipeline,
            MapSession mapSession,
            NativeSceneSession nativeSceneSession)
        {
            _externalAssetLoader = externalAssetLoader;
            _builderRegistry = builderRegistry;
            _poolPolicyRegistry = poolPolicyRegistry;
            _textureManager = textureManager;
            _materialManager = materialManager;
            _nodeHandlePool = nodeHandlePool;
            _streamingPipeline = streamingPipeline;
            _mapSession = mapSession;
            _nativeSceneSession = nativeSceneSession;
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
            var result = _mapSession.Load(
                mapURL,
                NotifyMapLoadError);
            if (!result.Success)
                return false;

            SceneManagerCamera?.MapChanged();
            OnMapChanged?.Invoke(result.RootNode);
            return true;
        }

        public bool ResetMap()
        {
            _mapSession.Reset();
            return true;
        }

        private void NotifyMapLoadError(
            ref string url,
            string error,
            SerializeAdapter.AdapterError errorType,
            ref bool retry) =>
            OnMapLoadError?.Invoke(
                ref url,
                error,
                errorType,
                ref retry);

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

            
            _nativeSceneSession.Initialize();
            try
            {
                _streamingPipeline.Initialize();

                // Start coroutines for asset loading
                StartCoroutine(_externalAssetLoader.Process());
            }
            catch
            {
                try
                {
                    _streamingPipeline.Shutdown();
                }
                finally
                {
                    _nativeSceneSession.Dispose();
                }

                throw;
            }

            return true;
        }

        public bool Uninitialize()
        {
            if (!_initialized)
                   return false;

            try
            {
                _streamingPipeline.Shutdown();
            }
            finally
            {
                try
                {
                    ResetMap();
                }
                finally
                {
                    try
                    {
                        _nativeSceneSession.Dispose();
                    }
                    finally
                    {
                        _initialized = false;
                    }
                }
            }

            // Dont do this as Unity wants to keep modules loaded
            //// Drop platform streamer
            //GizmoSDK.Gizmo3D.Platform.Uninitialize();

            return true;
        }

        public bool Init(bool loadMap = true)
        {
            if (_initialized)
                return true;

            // Initialize this manager
            if (!InitializeInternal())
                return false;

            _initialized = true;

            try
            {
                // Load the map
                if (loadMap && !LoadMap(_mapSession.MapUrl))
                {
                    Uninitialize();
                    return false;
                }
            }
            catch
            {
                if (_initialized)
                {
                    try
                    {
                        Uninitialize();
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogException(cleanupException);
                    }
                }

                throw;
            }

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
            if (_streamingPipeline.RenderInUpdate)
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



