using GizmoSDK.Coordinate;
using GizmoSDK.Gizmo3D;
using GizmoSDK.GizmoBase;

using Saab.Foundation.Unity.MapStreamer.Streaming;

namespace Saab.Foundation.Map
{
    public interface IMapSurfaceQueries
    {
        bool TryGetScreenGroundPosition(
            int x,
            int y,
            uint width,
            uint height,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result);
        bool TryGetGroundPosition(
            Vec3D globalPosition,
            Vec3 direction,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result);
        bool TryGetPosition(
            LatPos position,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result);
        bool TryGetPosition(
            CartPos position,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result);
        bool TryRefresh(
            in SurfaceQueryResult previous,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result);
        bool TryGetAltitude(
            LatPos position,
            SurfaceQueryOptions options,
            out double altitude);
    }

    public sealed class MapSurfaceQueries : IMapSurfaceQueries
    {
        private const double TriangleCacheAgeSeconds = 3.0;
        private readonly MapSession _session;
        private readonly IMapCoordinates _coordinates;
        private readonly MapViewContext _view;
        private readonly IStreamingClock _clock;

        public MapSurfaceQueries(
            MapSession session,
            IMapCoordinates coordinates,
            MapViewContext view,
            IStreamingClock clock)
        {
            _session = session;
            _coordinates = coordinates;
            _view = view;
            _clock = clock;
        }

        public bool TryGetScreenGroundPosition(
            int x,
            int y,
            uint width,
            uint height,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result)
        {
            if (!_view.TryGetScreenRay(
                x,
                y,
                width,
                height,
                out var position,
                out var direction))
            {
                result = default;
                return false;
            }

            return TryGetGroundPosition(
                position,
                direction,
                layers,
                options,
                out result);
        }

        public bool TryGetGroundPosition(
            Vec3D globalPosition,
            Vec3 direction,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result)
        {
            var ownsLock = _session.EnterEditLock();
            try
            {
                var snapshot = _session.Snapshot;
                if (!snapshot.IsInstalled)
                {
                    result = default;
                    return false;
                }

                var origin = new Vec3D();
                _view.TryGetNativeState(
                    out var camera,
                    out var lodFactor);
                if (camera != null && camera.IsValid())
                    origin = camera.Position;

                var intersector = new Intersector();
                try
                {
                    intersector.IntersectMask = ToMask(layers);
                    if (camera != null &&
                        camera.IsValid() &&
                        options.HasFlag(
                            SurfaceQueryOptions.FrustumCull))
                    {
                        intersector.SetCamera(camera);
                    }

                    intersector.StartPosition =
                        (Vec3)(globalPosition - origin);
                    intersector.Direction = direction;
                    var query =
                        IntersectQuery.ABC_TRI |
                        IntersectQuery.NEAREST_POINT |
                        (options.HasFlag(
                            SurfaceQueryOptions.AlignNormalToSurface)
                            ? IntersectQuery.NORMAL
                            : 0) |
                        (options.HasFlag(
                            SurfaceQueryOptions.WaitForData)
                            ? IntersectQuery.WAIT_FOR_DYNAMIC_DATA
                            : 0) |
                        (options.HasFlag(
                            SurfaceQueryOptions.UpdateData)
                            ? IntersectQuery.UPDATE_DYNAMIC_DATA
                            : 0);

                    if (intersector.Intersect(
                        snapshot.EffectiveRoot,
                        query,
                        lodFactor,
                        true,
                        origin))
                    {
                        var data =
                            intersector.GetResult().GetData(0);
                        return TryCreateHit(
                            data,
                            origin,
                            out result);
                    }
                }
                finally
                {
                    intersector.Dispose();
                }

                return TryCreateMiss(globalPosition, out result);
            }
            finally
            {
                _session.ExitEditLock(ownsLock);
            }
        }

        public bool TryGetPosition(
            LatPos position,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result)
        {
            if (!_coordinates.TryWorldToGlobal(
                position,
                out var global))
            {
                result = default;
                return false;
            }

            return TryCreateOrClamp(
                global,
                layers,
                options,
                out result);
        }

        public bool TryGetPosition(
            CartPos position,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result)
        {
            if (!_coordinates.TryWorldToGlobal(
                position,
                out var global))
            {
                result = default;
                return false;
            }

            return TryCreateOrClamp(
                global,
                layers,
                options,
                out result);
        }

        public bool TryRefresh(
            in SurfaceQueryResult previous,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result)
        {
            if (!_coordinates.TryLocalToGlobal(
                previous.Position,
                out var global))
            {
                result = default;
                return false;
            }

            var down = -global.EnuOrientation.GetCol(2);
            if (previous.HasTriangle &&
                _clock.SystemSeconds <=
                    previous.TriangleExpiresAt &&
                MapGeometry.IntersectRayTriangle(
                    global.Position,
                    down,
                    previous.TriangleA,
                    previous.TriangleB,
                    previous.TriangleC,
                    out var cachedHit))
            {
                return TryCreateCachedResult(
                    cachedHit,
                    previous,
                    out result);
            }

            if (TryGetGroundPosition(
                global.Position - 10000.0f * down,
                down,
                layers,
                options &
                    ~SurfaceQueryOptions.FrustumCull,
                out result) &&
                result.HasHit)
            {
                return true;
            }

            if (previous.HasTriangle &&
                MapGeometry.IntersectRayPlane(
                    global.Position,
                    down,
                    previous.TriangleA,
                    previous.TriangleB,
                    previous.TriangleC,
                    out var planeHit))
            {
                return TryCreateCachedResult(
                    planeHit,
                    previous,
                    out result);
            }

            return TryCreateMiss(global.Position, out result);
        }

        public bool TryGetAltitude(
            LatPos position,
            SurfaceQueryOptions options,
            out double altitude)
        {
            if (!TryGetPosition(
                    position,
                    SurfaceLayer.Ground,
                    options,
                    out var mapPosition) ||
                !_coordinates.TryLocalToWorld(
                    mapPosition.Position,
                    out LatPos worldPosition))
            {
                altitude = 0;
                return false;
            }

            altitude = worldPosition.Altitude;
            return true;
        }

        private bool TryCreateOrClamp(
            GlobalMapPosition global,
            SurfaceLayer layers,
            SurfaceQueryOptions options,
            out SurfaceQueryResult result)
        {
            if (layers == SurfaceLayer.None)
                return TryCreateMiss(global.Position, out result);

            var down = -global.EnuOrientation.GetCol(2);
            var rayStart = global.Position - 10000.0f * down;
            if (!TryGetGroundPosition(
                rayStart,
                down,
                layers,
                options,
                out result))
            {
                return false;
            }

            if (!options.HasFlag(
                    SurfaceQueryOptions.ConstrainSurface) ||
                !result.HasHit)
            {
                return true;
            }

            if (!_coordinates.TryLocalToGlobal(
                    result.Position,
                    out var clamped))
                return false;

            var delta =
                (Vec3)(global.Position - clamped.Position);
            if (Vec3.Dot(delta, result.Normal) > 0)
                return TryCreateMiss(global.Position, out result);

            return true;
        }

        private bool TryCreateHit(
            IntersectorData data,
            Vec3D origin,
            out SurfaceQueryResult result)
        {
            var globalPosition = data.coordinate + origin;
            if (!_coordinates.TryGlobalToLocal(
                globalPosition,
                out var localPosition))
            {
                result = default;
                return false;
            }

            var hasNormal =
                (data.resultMask & IntersectQuery.NORMAL) != 0;
            var normal = hasNormal
                ? data.normal
                : localPosition.EnuOrientation.GetCol(2);
            var hasTriangle =
                data.resultMask.HasFlag(IntersectQuery.ABC_TRI);
            result = new SurfaceQueryResult(
                localPosition,
                normal,
                true,
                hasNormal,
                hasTriangle ? data.a + origin : default,
                hasTriangle ? data.b + origin : default,
                hasTriangle ? data.c + origin : default,
                hasTriangle
                    ? _clock.SystemSeconds +
                        TriangleCacheAgeSeconds
                    : 0);
            return true;
        }

        private bool TryCreateMiss(
            Vec3D globalPosition,
            out SurfaceQueryResult result)
        {
            if (!_coordinates.TryGlobalToLocal(
                globalPosition,
                out var localPosition))
            {
                result = default;
                return false;
            }

            result = new SurfaceQueryResult(
                localPosition,
                localPosition.EnuOrientation.GetCol(2),
                false,
                false,
                default,
                default,
                default,
                0);
            return true;
        }

        private bool TryCreateCachedResult(
            Vec3D globalPosition,
            in SurfaceQueryResult previous,
            out SurfaceQueryResult result)
        {
            if (!_coordinates.TryGlobalToLocal(
                globalPosition,
                out var localPosition))
            {
                result = default;
                return false;
            }

            result = new SurfaceQueryResult(
                localPosition,
                previous.Normal,
                true,
                previous.HasNormal,
                previous.TriangleA,
                previous.TriangleB,
                previous.TriangleC,
                _clock.SystemSeconds + TriangleCacheAgeSeconds);
            return true;
        }

        private static IntersectMaskValue ToMask(
            SurfaceLayer layers)
        {
            var mask = IntersectMaskValue.NOTHING;
            if ((layers & SurfaceLayer.Ground) != 0)
                mask |= IntersectMaskValue.GROUND;
            if ((layers & SurfaceLayer.Building) != 0)
                mask |= IntersectMaskValue.BUILDING;
            return mask;
        }
    }
}
