using System;

using GizmoSDK.Coordinate;
using GizmoSDK.Gizmo3D;
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Map
{
    public enum MapProjection
    {
        Unknown,
        Plain,
        Utm,
        Geocentric,
        Sweref99,
    }

    [Flags]
    public enum SurfaceLayer
    {
        None = 0,
        Ground = 1 << 0,
        Building = 1 << 1,
    }

    [Flags]
    public enum SurfaceQueryOptions
    {
        None = 0,
        AlignNormalToSurface = 1 << 0,
        WaitForData = 1 << 1,
        LodQuality = 1 << 2,
        FrustumCull = 1 << 3,
        UpdateData = 1 << 4,
        ConstrainSurface = 1 << 5,
        Default = FrustumCull,
    }

    public sealed class MapMetadata
    {
        public static readonly MapMetadata Empty = new MapMetadata(
            MapProjection.Unknown,
            new Vec3D(),
            new CoordinateSystem(),
            new LatPos(0, 0, 0),
            new LatPos(0, 0, 0),
            string.Empty,
            0);

        public MapMetadata(
            MapProjection projection,
            Vec3D origin,
            CoordinateSystem coordinateSystem,
            LatPos southWestExtent,
            LatPos northEastExtent,
            string databaseSize,
            double maxLodDistance)
        {
            Projection = projection;
            Origin = origin;
            CoordinateSystem = coordinateSystem;
            SouthWestExtent = southWestExtent;
            NorthEastExtent = northEastExtent;
            DatabaseSize = databaseSize ?? string.Empty;
            MaxLodDistance = maxLodDistance;
        }

        public MapProjection Projection { get; }
        public Vec3D Origin { get; }
        public CoordinateSystem CoordinateSystem { get; }
        public LatPos SouthWestExtent { get; }
        public LatPos NorthEastExtent { get; }
        public string DatabaseSize { get; }
        public double MaxLodDistance { get; }
    }

    public readonly struct GlobalMapPosition
    {
        public GlobalMapPosition(Vec3D position, Matrix3 enuOrientation)
        {
            Position = position;
            EnuOrientation = enuOrientation;
        }

        public Vec3D Position { get; }
        public Matrix3 EnuOrientation { get; }
    }

    public readonly struct LocalMapPosition
    {
        public LocalMapPosition(
            RoiNode context,
            Vec3D position,
            Matrix3 enuOrientation)
        {
            Context = context;
            Position = position;
            EnuOrientation = enuOrientation;
        }

        public RoiNode Context { get; }
        public Vec3D Position { get; }
        public Matrix3 EnuOrientation { get; }
    }

    public readonly struct SurfaceQueryResult
    {
        public SurfaceQueryResult(
            LocalMapPosition position,
            Vec3 normal,
            bool hasHit,
            bool hasNormal,
            Vec3D triangleA,
            Vec3D triangleB,
            Vec3D triangleC,
            double triangleExpiresAt)
        {
            Position = position;
            Normal = normal;
            HasHit = hasHit;
            HasNormal = hasNormal;
            TriangleA = triangleA;
            TriangleB = triangleB;
            TriangleC = triangleC;
            TriangleExpiresAt = triangleExpiresAt;
        }

        public LocalMapPosition Position { get; }
        public Vec3 Normal { get; }
        public bool HasHit { get; }
        public bool HasNormal { get; }
        public Vec3D TriangleA { get; }
        public Vec3D TriangleB { get; }
        public Vec3D TriangleC { get; }
        public double TriangleExpiresAt { get; }
        public bool HasTriangle => TriangleExpiresAt > 0;
    }

    internal sealed class InstalledMapSnapshot
    {
        public static readonly InstalledMapSnapshot Empty =
            new InstalledMapSnapshot(
                string.Empty,
                null,
                null,
                MapMetadata.Empty,
                new CoordinateSystemMetaData());

        public InstalledMapSnapshot(
            string url,
            Node effectiveRoot,
            Roi topRoi,
            MapMetadata metadata,
            CoordinateSystemMetaData coordinateMetadata)
        {
            Url = url ?? string.Empty;
            EffectiveRoot = effectiveRoot;
            TopRoi = topRoi;
            Metadata = metadata ?? MapMetadata.Empty;
            CoordinateMetadata = coordinateMetadata;
        }

        public string Url { get; }
        public Node EffectiveRoot { get; }
        public Roi TopRoi { get; }
        public MapMetadata Metadata { get; }
        public CoordinateSystemMetaData CoordinateMetadata { get; }
        public bool IsInstalled =>
            EffectiveRoot != null && EffectiveRoot.IsValid();
    }

    internal enum MapLockScope
    {
        BorrowedEdit,
        OwnedEdit,
        TransitionedFromRender,
    }
}
