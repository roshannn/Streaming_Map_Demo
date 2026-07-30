using GizmoSDK.GizmoBase;

using Saab.Unity.Extensions;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    public partial class FoliageFeature
    {
        public Matrix4x4 GetClipToWorld(Camera camera)
        {
            var projection = GL.GetGPUProjectionMatrix(
                camera.projectionMatrix,
                false);
            projection[2, 3] = projection[3, 2] = 0.0f;
            projection[3, 3] = 1.0f;
            return Matrix4x4.Inverse(
                       projection * camera.worldToCameraMatrix) *
                   Matrix4x4.TRS(
                       new Vector3(0, 0, -projection[2, 2]),
                       UnityEngine.Quaternion.identity,
                       Vector3.one);
        }

        private static class PlacementParameterID
        {
            public static readonly int DepthTexture =
                Shader.PropertyToID("DepthTexture");
            public static readonly int WorldToScreen =
                Shader.PropertyToID("WorldToScreen");
            public static readonly int maxHeight =
                Shader.PropertyToID("maxHeight");
            public static readonly int OutputBuffer =
                Shader.PropertyToID("OutputBuffer");
            public static readonly int CameraPosition =
                Shader.PropertyToID("CameraPosition");
            public static readonly int CameraRightVector =
                Shader.PropertyToID("CameraRightVector");
            public static readonly int CameraForwardVector =
                Shader.PropertyToID("CameraForwardVector");
            public static readonly int frustumPlanes =
                Shader.PropertyToID("frustumPlanes");
            public static readonly int InputBuffer =
                Shader.PropertyToID("InputBuffer");
            public static readonly int ObjToWorld =
                Shader.PropertyToID("ObjToWorld");
            public static readonly int TerrainPoints =
                Shader.PropertyToID("TerrainPoints");
            public static readonly int BufferCount =
                Shader.PropertyToID("BufferCount");
            public static readonly int SplatMap =
                Shader.PropertyToID("SplatMap");
            public static readonly int Texture =
                Shader.PropertyToID("Texture");
            public static readonly int HeightSurface =
                Shader.PropertyToID("HeightSurface");
            public static readonly int heightResolution =
                Shader.PropertyToID("heightResolution");
            public static readonly int PixelToObjectCoord =
                Shader.PropertyToID("PixelToObjectCoord");
            public static readonly int PixelToWorld =
                Shader.PropertyToID("PixelToWorld");
            public static readonly int FeatureMap =
                Shader.PropertyToID("FeatureMap");
            public static readonly int FoliageData =
                Shader.PropertyToID("FoliageData");
            public static readonly int AngleDepth =
                Shader.PropertyToID("AngleDepth");
            public static readonly int FoliageCount =
                Shader.PropertyToID("FoliageCount");
            public static readonly int ScreenCoverage =
                Shader.PropertyToID("ScreenCoverage");
            public static readonly int AngleResolutionScale =
                Shader.PropertyToID("AngleResolutionScale");
        }

        private void PostCull()
        {
            _placement.SetBuffer(
                _kernelPostCull,
                PlacementParameterID.AngleDepth,
                _angleDepth);
            var groups = Mathf.CeilToInt(_angleDepth.count / 256f);
            _placement.Dispatch(
                _kernelPostCull,
                groups < 1 ? 1 : groups,
                1,
                1);
        }

        private Matrix4x4 LocalToWorldMatrix(GameObject gameObject)
        {
            if (!gameObject.TryGetComponent<NodeHandle>(out var handle))
                return Matrix4x4.identity;

            var center = handle.node.BoundaryCenter;
            if (!_mapCoordinates.TryGlobalToWorld(
                    center,
                    out GizmoSDK.Coordinate.LatPos latitudePosition))
            {
                return Matrix4x4.identity;
            }

            var enu = _mapCoordinates.GetEnuOrientation(latitudePosition);
            var east = enu * new Vec3(1, 0, 0);
            var up = enu * new Vec3(0, 0, 1);
            east = Vec3.Orthogonal(east, up);
            var north = up.Cross(east);

            var basis = FromBasis(
                east.ToVector3(),
                up.ToVector3(),
                -north.ToVector3());
            return gameObject.transform.localToWorldMatrix * basis;
        }

        private static Matrix4x4 FromBasis(
            Vector3 right,
            Vector3 up,
            Vector3 forward)
        {
            var matrix = Matrix4x4.identity;
            matrix.SetColumn(
                0,
                new Vector4(right.x, right.y, right.z, 0f));
            matrix.SetColumn(
                1,
                new Vector4(up.x, up.y, up.z, 0f));
            matrix.SetColumn(
                2,
                new Vector4(
                    forward.x,
                    forward.y,
                    forward.z,
                    0f));
            return matrix;
        }

        private void PreCull()
        {
            foreach (var item in _items)
            {
                var gameObject = item.Object;
                if (!gameObject.activeInHierarchy)
                    continue;

                var groups =
                    Mathf.CeilToInt(item.TerrainPoints.count / 128f);
                _placement.SetBuffer(
                    _KernelPreCull,
                    PlacementParameterID.AngleDepth,
                    _angleDepth);
                _placement.SetBuffer(
                    _KernelPreCull,
                    PlacementParameterID.InputBuffer,
                    item.TerrainPoints);
                _placement.SetMatrix(
                    PlacementParameterID.ObjToWorld,
                    gameObject.transform.localToWorldMatrix);
                _placement.Dispatch(
                    _KernelPreCull,
                    groups < 1 ? 1 : groups,
                    1,
                    1);
            }
        }

        public ComputeBuffer Cull(
            Vector4[] frustum,
            Camera camera,
            float maxHeight,
            RenderTexture depth,
            ComputeBuffer foliageData,
            float screenCoverage)
        {
            _pointCloud.SetCounterValue(0);
            var worldToScreen =
                camera.projectionMatrix * camera.worldToCameraMatrix;

            _placement.SetTexture(
                _kernelCull,
                PlacementParameterID.DepthTexture,
                depth);
            _placement.SetTexture(
                _KernelPreCull,
                PlacementParameterID.DepthTexture,
                depth);
            _placement.SetMatrix(
                PlacementParameterID.WorldToScreen,
                worldToScreen);
            _placement.SetFloat(
                PlacementParameterID.maxHeight,
                maxHeight);
            _placement.SetVector(
                PlacementParameterID.CameraPosition,
                camera.transform.position);
            _placement.SetVector(
                PlacementParameterID.CameraRightVector,
                camera.transform.right);
            _placement.SetVector(
                PlacementParameterID.CameraForwardVector,
                camera.transform.forward);
            _placement.SetVectorArray(
                PlacementParameterID.frustumPlanes,
                frustum);

            const float fovTolerance = 3f;
            var verticalView = camera.fieldOfView;
            var horizontalView = Camera.VerticalToHorizontalFieldOfView(
                verticalView,
                camera.aspect);
            _fov = new Vector2(
                horizontalView + fovTolerance,
                verticalView + fovTolerance);
            _placement.SetVector("Fov", _fov);

            _placement.SetBuffer(
                _kernelCull,
                PlacementParameterID.OutputBuffer,
                _pointCloud);
            _placement.SetBuffer(
                _kernelCull,
                PlacementParameterID.FoliageData,
                foliageData);
            _placement.SetBuffer(
                _KernelPreCull,
                PlacementParameterID.FoliageData,
                foliageData);
            _placement.SetInt(
                PlacementParameterID.FoliageCount,
                foliageData.count);
            _placement.SetFloat(
                PlacementParameterID.ScreenCoverage,
                screenCoverage);

            PreCull();
            foreach (var item in _items)
            {
                var gameObject = item.Object;
                if (!gameObject.activeInHierarchy)
                    continue;

                var groups =
                    Mathf.CeilToInt(item.TerrainPoints.count / 128f);
                _placement.SetBuffer(
                    _kernelCull,
                    PlacementParameterID.AngleDepth,
                    _angleDepth);
                _placement.SetBuffer(
                    _kernelCull,
                    PlacementParameterID.InputBuffer,
                    item.TerrainPoints);
                _placement.SetMatrix(
                    PlacementParameterID.ObjToWorld,
                    gameObject.transform.localToWorldMatrix);
                _placement.Dispatch(
                    _kernelCull,
                    groups < 1 ? 1 : groups,
                    1,
                    1);
            }

            PostCull();
            return _pointCloud;
        }
    }
}
