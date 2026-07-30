using System;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    public interface IMapEventHandler<TEvent>
    {
        void Handle(in TEvent mapEvent);
    }

    public interface IMapModule
    {
        void Initialize();
        void Shutdown();
    }

    public readonly struct TerrainAddedEvent
    {
        public TerrainAddedEvent(in TerrainModuleContext terrain)
        {
            Terrain = terrain;
        }

        public TerrainModuleContext Terrain { get; }
    }

    public readonly struct TerrainRemovedEvent
    {
        public TerrainRemovedEvent(in TerrainRemovalContext terrain)
        {
            Terrain = terrain;
        }

        public TerrainRemovalContext Terrain { get; }
    }

    public readonly struct StreamingFrameCompletedEvent
    {
        public StreamingFrameCompletedEvent(
            in StreamingFrameModuleContext frame)
        {
            Frame = frame;
        }

        public StreamingFrameModuleContext Frame { get; }
    }

    public interface ITerrainAddedHandler
    {
        void OnTerrainAdded(in TerrainModuleContext context);
    }

    public interface ITerrainRemovedHandler
    {
        void OnTerrainRemoved(in TerrainRemovalContext context);
    }

    public interface IStreamingFrameCompletedHandler
    {
        void OnStreamingFrameCompleted(
            in StreamingFrameModuleContext context);
    }

    public readonly struct TerrainModuleIdentity :
        IEquatable<TerrainModuleIdentity>
    {
        public TerrainModuleIdentity(int gameObjectInstanceId, byte nodeVersion)
        {
            GameObjectInstanceId = gameObjectInstanceId;
            NodeVersion = nodeVersion;
        }

        public int GameObjectInstanceId { get; }
        public byte NodeVersion { get; }

        public bool Equals(TerrainModuleIdentity other) =>
            GameObjectInstanceId == other.GameObjectInstanceId &&
            NodeVersion == other.NodeVersion;

        public override bool Equals(object obj) =>
            obj is TerrainModuleIdentity other && Equals(other);

        public override int GetHashCode() =>
            (GameObjectInstanceId * 397) ^ NodeVersion;

        public override string ToString() =>
            $"{GameObjectInstanceId}:{NodeVersion}";
    }

    public readonly struct TerrainModuleContext
    {
        public TerrainModuleContext(
            TerrainModuleIdentity identity,
            GameObject gameObject,
            NodeHandle nodeHandle,
            Mesh mesh,
            MeshRenderer renderer,
            Texture2D texture,
            Texture2D featureTexture,
            Texture2D surfaceHeightTexture,
            Bounds bounds,
            bool isAsset)
        {
            Identity = identity;
            GameObject = gameObject;
            NodeHandle = nodeHandle;
            Mesh = mesh;
            Renderer = renderer;
            Texture = texture;
            FeatureTexture = featureTexture;
            SurfaceHeightTexture = surfaceHeightTexture;
            Bounds = bounds;
            IsAsset = isAsset;
        }

        public TerrainModuleIdentity Identity { get; }
        public GameObject GameObject { get; }
        public NodeHandle NodeHandle { get; }
        public Mesh Mesh { get; }
        public MeshRenderer Renderer { get; }
        public Texture2D Texture { get; }
        public Texture2D FeatureTexture { get; }
        public Texture2D SurfaceHeightTexture { get; }
        public Transform Transform => GameObject != null
            ? GameObject.transform
            : null;
        public Bounds Bounds { get; }
        public bool IsAsset { get; }
    }

    public readonly struct TerrainRemovalContext
    {
        public TerrainRemovalContext(in TerrainModuleContext terrain)
        {
            Terrain = terrain;
        }

        public TerrainModuleContext Terrain { get; }
        public TerrainModuleIdentity Identity => Terrain.Identity;
        public GameObject GameObject => Terrain.GameObject;
    }

    public readonly struct StreamingFrameModuleContext
    {
        public StreamingFrameModuleContext(
            double renderTime,
            TimeSpan elapsed)
        {
            RenderTime = renderTime;
            Elapsed = elapsed;
        }

        public double RenderTime { get; }
        public TimeSpan Elapsed { get; }
    }
}
