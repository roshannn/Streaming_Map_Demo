using GizmoSDK.Coordinate;
using GizmoSDK.Gizmo3D;
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Map
{
    public interface IMapCoordinates
    {
        bool TryGlobalToWorld(Vec3D globalPosition, out LatPos position);
        bool TryGlobalToWorld(Vec3D globalPosition, out CartPos position);
        bool TryWorldToGlobal(LatPos position, out GlobalMapPosition result);
        bool TryWorldToGlobal(CartPos position, out GlobalMapPosition result);
        bool TryGlobalToLocal(
            Vec3D globalPosition,
            out LocalMapPosition result);
        bool TryLocalToGlobal(
            LocalMapPosition localPosition,
            out GlobalMapPosition result);
        bool TryLocalToWorld(
            LocalMapPosition localPosition,
            out LatPos position);
        bool TryLocalToWorld(
            LocalMapPosition localPosition,
            out CartPos position);
        Matrix3 GetEnuOrientation(Vec3D globalPosition);
        Matrix3 GetEnuOrientation(LatPos position);
    }

    public sealed class MapCoordinates : IMapCoordinates
    {
        private static readonly Matrix3 FlatEnuOrientation =
            new Matrix3(
                new Vec3(1, 0, 0),
                new Vec3(0, 0, -1),
                new Vec3(0, 1, 0));

        private readonly MapSession _session;
        private readonly Coordinate _converter = new Coordinate();
        private readonly object _converterSync = new object();

        public MapCoordinates(MapSession session)
        {
            _session = session;
        }

        public bool TryGlobalToWorld(
            Vec3D globalPosition,
            out LatPos position)
        {
            lock (_converterSync)
            {
                var snapshot = _session.Snapshot;
                if (!SetGlobalPosition(snapshot, globalPosition))
                {
                    position = default;
                    return false;
                }

                return _converter.GetLatPos(out position);
            }
        }

        public bool TryGlobalToWorld(
            Vec3D globalPosition,
            out CartPos position)
        {
            lock (_converterSync)
            {
                var snapshot = _session.Snapshot;
                if (!SetGlobalPosition(snapshot, globalPosition))
                {
                    position = default;
                    return false;
                }

                return _converter.GetCartPos(out position);
            }
        }

        public bool TryWorldToGlobal(
            LatPos position,
            out GlobalMapPosition result)
        {
            lock (_converterSync)
            {
                var snapshot = _session.Snapshot;
                ConfigureConverter(snapshot);
                _converter.SetLatPos(position);
                return TryReadGlobalPosition(snapshot, out result);
            }
        }

        public bool TryWorldToGlobal(
            CartPos position,
            out GlobalMapPosition result)
        {
            lock (_converterSync)
            {
                var snapshot = _session.Snapshot;
                ConfigureConverter(snapshot);
                _converter.SetCartPos(position);
                return TryReadGlobalPosition(snapshot, out result);
            }
        }

        public bool TryGlobalToLocal(
            Vec3D globalPosition,
            out LocalMapPosition result)
        {
            var ownsLock = _session.EnterEditLock();
            try
            {
                var snapshot = _session.Snapshot;
                if (snapshot.TopRoi == null)
                {
                    result = default;
                    return false;
                }

                var roi = snapshot.TopRoi.GetClosestRoiNode(globalPosition);
                if (roi == null || !roi.IsValid())
                {
                    result = default;
                    return false;
                }

                result = new LocalMapPosition(
                    roi,
                    globalPosition - roi.Position,
                    GetEnuOrientation(globalPosition));
                return true;
            }
            finally
            {
                _session.ExitEditLock(ownsLock);
            }
        }

        public bool TryLocalToGlobal(
            LocalMapPosition localPosition,
            out GlobalMapPosition result)
        {
            var ownsLock = _session.EnterEditLock();
            try
            {
                var globalPosition = localPosition.Position;
                var context = localPosition.Context;
                if (context != null)
                {
                    if (!context.IsValid())
                    {
                        result = default;
                        return false;
                    }

                    globalPosition += context.Position;
                }

                result = new GlobalMapPosition(
                    globalPosition,
                    localPosition.EnuOrientation);
                return true;
            }
            finally
            {
                _session.ExitEditLock(ownsLock);
            }
        }

        public bool TryLocalToWorld(
            LocalMapPosition localPosition,
            out LatPos position)
        {
            if (!TryLocalToGlobal(localPosition, out var global))
            {
                position = default;
                return false;
            }

            return TryGlobalToWorld(global.Position, out position);
        }

        public bool TryLocalToWorld(
            LocalMapPosition localPosition,
            out CartPos position)
        {
            if (!TryLocalToGlobal(localPosition, out var global))
            {
                position = default;
                return false;
            }

            return TryGlobalToWorld(global.Position, out position);
        }

        public Matrix3 GetEnuOrientation(Vec3D globalPosition)
        {
            var metadata = _session.Snapshot.Metadata;
            switch (metadata.Projection)
            {
                case MapProjection.Utm:
                case MapProjection.Sweref99:
                    return FlatEnuOrientation;
                case MapProjection.Geocentric:
                    return Coordinate.GetOrientationMatrix(
                        new CartPos(
                            globalPosition.x + metadata.Origin.x,
                            globalPosition.y + metadata.Origin.y,
                            globalPosition.z + metadata.Origin.z));
                default:
                    return new Matrix3();
            }
        }

        public Matrix3 GetEnuOrientation(LatPos position)
        {
            var snapshot = _session.Snapshot;
            switch (snapshot.Metadata.Projection)
            {
                case MapProjection.Utm:
                case MapProjection.Sweref99:
                    return FlatEnuOrientation;
                case MapProjection.Geocentric:
                    lock (_converterSync)
                    {
                        ConfigureConverter(snapshot);
                        _converter.SetLatPos(position);
                        if (_converter.GetCartPos(out var cartPosition))
                            return Coordinate.GetOrientationMatrix(cartPosition);
                        return new Matrix3();
                    }
                default:
                    return new Matrix3();
            }
        }

        private bool SetGlobalPosition(
            InstalledMapSnapshot snapshot,
            Vec3D position)
        {
            ConfigureConverter(snapshot);
            var origin = snapshot.Metadata.Origin;
            switch (snapshot.Metadata.Projection)
            {
                case MapProjection.Utm:
                    _converter.SetUTMPos(
                        new UTMPos(
                            snapshot.CoordinateMetadata.Zone(),
                            snapshot.CoordinateMetadata.North(),
                            -(position.z + origin.z),
                            position.x + origin.x,
                            position.y + origin.y));
                    return true;
                case MapProjection.Geocentric:
                    _converter.SetCartPos(
                        new CartPos(
                            position.x + origin.x,
                            position.y + origin.y,
                            position.z + origin.z));
                    return true;
                case MapProjection.Sweref99:
                    _converter.SetProjPos(
                        new ProjPos(
                            -(position.z + origin.z),
                            position.x + origin.x,
                            position.y + origin.y),
                        FlatGaussProjection.SWEREF99);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryReadGlobalPosition(
            InstalledMapSnapshot snapshot,
            out GlobalMapPosition result)
        {
            var origin = snapshot.Metadata.Origin;
            Vec3D position;
            switch (snapshot.Metadata.Projection)
            {
                case MapProjection.Utm:
                    if (!_converter.GetUTMPos(out var utmPosition))
                    {
                        result = default;
                        return false;
                    }

                    position = new Vec3D(
                        utmPosition.Easting,
                        utmPosition.H,
                        -utmPosition.Northing) - origin;
                    break;
                case MapProjection.Geocentric:
                    if (!_converter.GetCartPos(out var cartPosition))
                    {
                        result = default;
                        return false;
                    }

                    position = new Vec3D(
                        cartPosition.X,
                        cartPosition.Y,
                        cartPosition.Z) - origin;
                    break;
                case MapProjection.Sweref99:
                    if (!_converter.GetProjPos(
                        out var projectedPosition,
                        FlatGaussProjection.SWEREF99))
                    {
                        result = default;
                        return false;
                    }

                    position = new Vec3D(
                        projectedPosition.Y,
                        projectedPosition.H,
                        -projectedPosition.X) - origin;
                    break;
                default:
                    result = default;
                    return false;
            }

            result = new GlobalMapPosition(
                position,
                GetEnuOrientation(position));
            return true;
        }

        private void ConfigureConverter(InstalledMapSnapshot snapshot)
        {
            if (snapshot.Metadata.Projection == MapProjection.Utm)
            {
                _converter.PrefUTMZone =
                    snapshot.CoordinateMetadata.Zone();
                _converter.PrefUTMHemisphere =
                    snapshot.CoordinateMetadata.North() ? 1 : -1;
                return;
            }

            _converter.PrefUTMZone = -1;
            _converter.PrefUTMHemisphere = 0;
        }
    }
}
