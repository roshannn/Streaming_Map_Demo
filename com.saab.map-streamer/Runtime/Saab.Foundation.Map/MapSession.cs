using System.Threading;

using GizmoSDK.Coordinate;
using GizmoSDK.Gizmo3D;
using GizmoSDK.GizmoBase;

using Saab.Foundation.Unity.MapStreamer.Streaming;

namespace Saab.Foundation.Map
{
    public interface IMapSession
    {
        bool IsInstalled { get; }
        string Url { get; }
        MapMetadata Metadata { get; }
        Node Install(string url, Node source);
        void Reset();
    }

    public sealed class MapSession : IMapSession
    {
        private const string ProjectionFlat = "Flat Earth";
        private const string ProjectionSphere = "Sphere";
        private const string ProjectionUtm = "UTM";
        private const string ProjectionSweref99 = "SWEREF99";
        private const string ProjectionKey = "DbI-Projection";
        private const string CoordinateSystemKey = "DbI-CoordSystem";
        private const string OriginKey = "DbI-Database Origin";
        private const string SouthWestKey = "DbI-Database SWpos";
        private const string NorthEastKey = "DbI-Database NEpos";
        private const string DatabaseSizeKey = "DbI-SZ";
        private const string MaxLodRangeKey = "DbI-LR";

        private readonly IStreamingLock _streamingLock;
        private InstalledMapSnapshot _snapshot = InstalledMapSnapshot.Empty;

        public MapSession(IStreamingLock streamingLock)
        {
            _streamingLock = streamingLock;
        }

        public bool IsInstalled => Snapshot.IsInstalled;
        public string Url => Snapshot.Url;
        public MapMetadata Metadata => Snapshot.Metadata;

        internal InstalledMapSnapshot Snapshot =>
            Volatile.Read(ref _snapshot);

        public Node Install(string url, Node source)
        {
            var ownsLock = EnterEditLock();
            try
            {
                var next = CreateSnapshot(url, source);
                Volatile.Write(ref _snapshot, next);
                return next.EffectiveRoot;
            }
            finally
            {
                ExitEditLock(ownsLock);
            }
        }

        public void Reset()
        {
            var ownsLock = EnterEditLock();
            try
            {
                Volatile.Write(ref _snapshot, InstalledMapSnapshot.Empty);
            }
            finally
            {
                ExitEditLock(ownsLock);
            }
        }

        internal MapLockScope EnterEditLock()
        {
            if (_streamingLock.IsOwnedByCurrentThread)
            {
                var wasRenderLock = _streamingLock.IsRenderLock;
                if (wasRenderLock &&
                    !_streamingLock.ChangeToEdit())
                    throw new System.InvalidOperationException(
                        "Could not transition the streaming lock to edit mode.");

                return wasRenderLock
                    ? MapLockScope.TransitionedFromRender
                    : MapLockScope.BorrowedEdit;
            }

            _streamingLock.AcquireEdit();
            return MapLockScope.OwnedEdit;
        }

        internal void ExitEditLock(MapLockScope scope)
        {
            switch (scope)
            {
                case MapLockScope.OwnedEdit:
                    _streamingLock.Release();
                    break;
                case MapLockScope.TransitionedFromRender:
                    if (!_streamingLock.ChangeToRender())
                        throw new System.InvalidOperationException(
                            "Could not restore the streaming lock to render mode.");
                    break;
            }
        }

        private static InstalledMapSnapshot CreateSnapshot(
            string url,
            Node source)
        {
            if (source == null || !source.IsValid() || !source.HasDbInfo())
                return new InstalledMapSnapshot(
                    url,
                    source,
                    null,
                    MapMetadata.Empty,
                    new CoordinateSystemMetaData());

            ParseMetadata(
                source,
                out var metadata,
                out var coordinateMetadata);

            var topRoi = FindTopRoi(source);
            Node effectiveRoot = source;
            if (topRoi == null)
            {
                topRoi = new Roi();
                var roiNode = new RoiNode
                {
                    LoadDistance = 2 * metadata.MaxLodDistance,
                    PurgeDistance = 2 * metadata.MaxLodDistance
                };

                if (!string.IsNullOrEmpty(url))
                {
                    roiNode.AddNode(new DynamicLoader
                    {
                        NodeURL = url
                    });
                }
                else
                {
                    roiNode.AddNode(source);
                }

                topRoi.AddNode(roiNode);
                effectiveRoot = topRoi;
            }

            return new InstalledMapSnapshot(
                url,
                effectiveRoot,
                topRoi,
                metadata,
                coordinateMetadata);
        }

        private static void ParseMetadata(
            Node source,
            out MapMetadata metadata,
            out CoordinateSystemMetaData coordinateMetadata)
        {
            var projection = MapProjection.Unknown;
            var origin = new Vec3D();
            var coordinateSystem = new CoordinateSystem();
            coordinateMetadata = new CoordinateSystemMetaData();

            var projectionName = source.HasDbInfo(ProjectionKey)
                ? source.GetDbInfo(ProjectionKey).AsString()
                : string.Empty;
            if (projectionName == ProjectionUtm &&
                source.HasDbInfo(OriginKey))
            {
                projection = MapProjection.Utm;
                UTMPos utmOrigin = source.GetDbInfo(OriginKey);
                origin = new Vec3D(
                    utmOrigin.Easting,
                    utmOrigin.H,
                    -utmOrigin.Northing);
                coordinateMetadata =
                    new CoordinateSystemMetaData(
                        utmOrigin.Zone,
                        utmOrigin.North);
                coordinateSystem = new CoordinateSystem(
                    Datum.WGS84_ELLIPSOID,
                    FlatGaussProjection.UTM,
                    CoordinateType.UTM);
            }
            else if (projectionName == ProjectionFlat &&
                source.HasDbInfo(OriginKey))
            {
                projection = MapProjection.Plain;
                origin = (Vec3D)source.GetDbInfo(OriginKey).GetVec3();
            }
            else if (projectionName == ProjectionSphere &&
                source.HasDbInfo(OriginKey))
            {
                projection = MapProjection.Geocentric;
                CartPos cartOrigin = source.GetDbInfo(OriginKey);
                origin = new Vec3D(
                    cartOrigin.X,
                    cartOrigin.Y,
                    cartOrigin.Z);
                coordinateSystem = new CoordinateSystem(
                    Datum.WGS84_ELLIPSOID,
                    FlatGaussProjection.NOT_DEFINED,
                    CoordinateType.GEOCENTRIC);
            }
            else if (projectionName == ProjectionSweref99 &&
                source.HasDbInfo(OriginKey))
            {
                projection = MapProjection.Sweref99;
                ProjPos projectedOrigin = source.GetDbInfo(OriginKey);
                origin = new Vec3D(
                    projectedOrigin.Y,
                    projectedOrigin.H,
                    -projectedOrigin.X);
                coordinateSystem = new CoordinateSystem(
                    Datum.GRS80_ELLIPSOID,
                    FlatGaussProjection.SWEREF99,
                    CoordinateType.PROJECTED);
            }

            if (source.HasDbInfo(CoordinateSystemKey))
                coordinateSystem =
                    new CoordinateSystem(
                        source.GetDbInfo(CoordinateSystemKey));

            var southWest = source.HasDbInfo(SouthWestKey)
                ? (LatPos)source.GetDbInfo(SouthWestKey)
                : new LatPos(0, 0, 0);
            var northEast = source.HasDbInfo(NorthEastKey)
                ? (LatPos)source.GetDbInfo(NorthEastKey)
                : new LatPos(0, 0, 0);
            var databaseSize = source.HasDbInfo(DatabaseSizeKey)
                ? source.GetDbInfo(DatabaseSizeKey).AsString()
                : string.Empty;
            var maxLodDistance = source.HasDbInfo(MaxLodRangeKey)
                ? source.GetDbInfo(MaxLodRangeKey).GetNumber()
                : 0;

            metadata = new MapMetadata(
                projection,
                origin,
                coordinateSystem,
                southWest,
                northEast,
                databaseSize,
                maxLodDistance);
        }

        private static Roi FindTopRoi(Node map)
        {
            if (map == null)
                return null;

            if (map is Roi roi)
                return roi;

            if (!(map is Group group))
                return null;

            foreach (Node child in group)
            {
                var nested = FindTopRoi(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
