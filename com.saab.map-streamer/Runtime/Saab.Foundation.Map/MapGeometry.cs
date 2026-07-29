using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Map
{
    public static class MapGeometry
    {
        private const double Epsilon = 1e-8;

        public static bool IntersectRayTriangle(
            Vec3D origin,
            Vec3D direction,
            Vec3D vertex0,
            Vec3D vertex1,
            Vec3D vertex2,
            out Vec3D intersection)
        {
            var edge1 = vertex1 - vertex0;
            var edge2 = vertex2 - vertex0;
            var cross = direction.Cross(edge2);
            var determinant = cross.Dot(edge1);
            if (determinant > -Epsilon && determinant < Epsilon)
            {
                intersection = default;
                return false;
            }

            var inverseDeterminant = 1.0 / determinant;
            var offset = origin - vertex0;
            var u = cross.Dot(offset) * inverseDeterminant;
            if (u < 0 || u > 1)
            {
                intersection = default;
                return false;
            }

            var secondCross = offset.Cross(edge1);
            var v = secondCross.Dot(direction) * inverseDeterminant;
            if (v < 0 || u + v > 1)
            {
                intersection = default;
                return false;
            }

            var distance =
                secondCross.Dot(edge2) * inverseDeterminant;
            intersection = origin + distance * direction;
            return true;
        }

        public static bool IntersectRayPlane(
            Vec3D origin,
            Vec3D direction,
            Vec3D vertex0,
            Vec3D vertex1,
            Vec3D vertex2,
            out Vec3D intersection)
        {
            var normal =
                (vertex1 - vertex0).Cross(vertex2 - vertex0);
            normal.Normalize();
            var denominator = Vec3D.Dot(normal, direction);
            if (denominator > -Epsilon && denominator < Epsilon)
            {
                intersection = default;
                return false;
            }

            var planeDistance = -Vec3D.Dot(normal, vertex0);
            var distance =
                -(Vec3D.Dot(normal, origin) + planeDistance) /
                denominator;
            intersection = origin + distance * direction;
            return true;
        }
    }
}
