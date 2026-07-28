using System;

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.Streaming;

using NativeCamera = GizmoSDK.Gizmo3D.Camera;

namespace Saab.Foundation.Unity.MapStreamer.GizmoAdapter
{
    public interface IGizmoSceneCallbacks
    {
        void AttachCamera(NativeCamera camera);
        void DetachCamera();
        void SetLodFactor(float lodFactor);
    }

    public sealed class GizmoStreamingBackend : IStreamingBackend, IDisposable
    {
        private readonly IGizmoSceneCallbacks _callbacks;
        private readonly IStreamingLock _streamingLock;
        private Scene _scene;
        private NativeCamera _camera;
        private Context _context;
        private CullTraverseAction _traverseAction;

        public GizmoStreamingBackend(
            IGizmoSceneCallbacks callbacks,
            IStreamingLock streamingLock)
        {
            _callbacks = callbacks;
            _streamingLock = streamingLock;
        }

        public bool IsInitialized => _camera != null;

        public bool Initialize()
        {
            if (IsInitialized)
                return true;
            if (!GizmoSdkRuntime.Initialize())
                return false;

            try
            {
                _streamingLock.AcquireEdit();
                try
                {
                    _camera = new PerspCamera("MapStreamer")
                    {
                        RoiPosition = true,
                    };
                    _scene = new Scene("MapStreamer");
                    _context = new Context();
                    _traverseAction = new CullTraverseAction();
                    _camera.Scene = _scene;
                    _callbacks.AttachCamera(_camera);
                    return true;
                }
                catch
                {
                    DisposeNativeResources();
                    throw;
                }
                finally
                {
                    _streamingLock.Release();
                }
            }
            catch
            {
                GizmoSdkRuntime.Shutdown();
                throw;
            }
        }

        public void Render(in StreamingFrame frame)
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "The Gizmo streaming backend is not initialized.");

            Lod.SetLODFactor(_context, frame.LodFactor);
            _callbacks.SetLodFactor(frame.LodFactor);
            _context.CurrentRenderTime = frame.RenderTime;

            if (_camera is PerspCamera perspectiveCamera)
            {
                perspectiveCamera.VerticalFOV = frame.VerticalFieldOfView;
                perspectiveCamera.HorizontalFOV =
                    2f * UnityEngine.Mathf.Atan(
                        UnityEngine.Mathf.Tan(
                            frame.VerticalFieldOfView *
                            UnityEngine.Mathf.Deg2Rad / 2f) *
                        frame.Aspect) *
                    UnityEngine.Mathf.Rad2Deg;
                perspectiveCamera.NearClipPlane = frame.NearClipPlane;
                perspectiveCamera.FarClipPlane = frame.FarClipPlane;
            }

            _camera.Transform = ToZFlippedMatrix(frame.WorldToCameraMatrix);
            _camera.Position =
                new Vec3D(frame.GlobalX, frame.GlobalY, frame.GlobalZ);
            _camera.Render(_context, 1000, 1000, 1000, _traverseAction);
        }

        public void AddNode(Node node)
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "The Gizmo streaming backend is not initialized.");

            _scene.AddNode(node);
#if DEBUG
            _scene.Debug();
#endif
        }

        public void ClearScene()
        {
            _scene?.RemoveAllNodes();
        }

        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            try
            {
                _streamingLock.AcquireEdit();
                try
                {
                    DisposeNativeResources();
                }
                finally
                {
                    _streamingLock.Release();
                }
            }
            finally
            {
                GizmoSdkRuntime.Shutdown();
            }
        }

        public void Dispose() => Shutdown();

        private void DisposeNativeResources()
        {
            _callbacks.DetachCamera();
            _traverseAction?.Dispose();
            _camera?.Dispose();
            _context?.Dispose();
            _scene?.Dispose();

            _traverseAction = null;
            _camera = null;
            _context = null;
            _scene = null;
        }

        private static Matrix4 ToZFlippedMatrix(UnityEngine.Matrix4x4 matrix)
        {
            return new Matrix4
            {
                v11 = matrix.m00,
                v12 = matrix.m01,
                v13 = -matrix.m02,
                v14 = matrix.m03,
                v21 = matrix.m10,
                v22 = matrix.m11,
                v23 = -matrix.m12,
                v24 = matrix.m13,
                v31 = -matrix.m20,
                v32 = -matrix.m21,
                v33 = matrix.m22,
                v34 = -matrix.m23,
                v41 = matrix.m30,
                v42 = matrix.m31,
                v43 = -matrix.m32,
                v44 = matrix.m33,
            };
        }
    }
}
