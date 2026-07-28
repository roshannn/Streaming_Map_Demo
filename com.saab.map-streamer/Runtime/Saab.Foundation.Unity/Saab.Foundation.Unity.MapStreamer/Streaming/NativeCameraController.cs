using System;

using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Unity.Extensions;

using UnityEngine;

using NativeCamera = GizmoSDK.Gizmo3D.Camera;

namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    internal sealed class NativeCameraController
    {
        private NativeCamera _nativeCamera;
        private MapControl _mapControl;

        public IStreamingCamera StreamingCamera { get; private set; }
        public bool IsReady => _nativeCamera != null && StreamingCamera != null;

        public void Initialize(
            Scene nativeScene,
            MapControl mapControl)
        {
            if (nativeScene == null)
                throw new ArgumentNullException(nameof(nativeScene));
            if (mapControl == null)
                throw new ArgumentNullException(nameof(mapControl));
            if (_nativeCamera != null)
                return;

            var nativeCamera = new PerspCamera("Test")
            {
                RoiPosition = true,
                Scene = nativeScene,
            };

            _nativeCamera = nativeCamera;
            _mapControl = mapControl;
            mapControl.Camera = nativeCamera;
            SynchronizeInitialStateIfReady();
        }

        public void SetStreamingCamera(IStreamingCamera streamingCamera)
        {
            StreamingCamera = streamingCamera ??
                throw new ArgumentNullException(nameof(streamingCamera));
            SynchronizeInitialStateIfReady();
        }

        public void Render(
            Context nativeContext,
            CullTraverseAction traverseAction)
        {
            EnsureReady();
            _nativeCamera.Render(
                nativeContext,
                1000,
                1000,
                1000,
                traverseAction);
        }

        public void Dispose(Context nativeContext)
        {
            if (_nativeCamera == null)
                return;

#if DEBUG_CAMERA
            _nativeCamera.Debug(nativeContext, false);
#endif

            if (_mapControl != null &&
                ReferenceEquals(_mapControl.Camera, _nativeCamera))
            {
                _mapControl.Camera = null;
            }

            _nativeCamera.Dispose();
            _nativeCamera = null;
            _mapControl = null;
        }

        public double Update(double renderTime)
        {
            EnsureReady();

            renderTime = StreamingCamera.Update(renderTime);
            SynchronizePose();
            return renderTime;
        }

        private void SynchronizeInitialStateIfReady()
        {
            if (!IsReady)
                return;

            var unityCamera = StreamingCamera.UnityCamera;
            if (unityCamera == null)
                throw new InvalidOperationException(
                    "The assigned streaming camera has no Unity camera.");

            if (_nativeCamera is PerspCamera perspectiveCamera)
            {
                perspectiveCamera.VerticalFOV = unityCamera.fieldOfView;
                perspectiveCamera.HorizontalFOV =
                    2 * Mathf.Atan(
                        Mathf.Tan(
                            unityCamera.fieldOfView * Mathf.Deg2Rad / 2) *
                        unityCamera.aspect) *
                    Mathf.Rad2Deg;
                perspectiveCamera.NearClipPlane = unityCamera.nearClipPlane;
                perspectiveCamera.FarClipPlane = unityCamera.farClipPlane;
            }

            SynchronizePose();
        }

        private void SynchronizePose()
        {
            var unityCamera = StreamingCamera.UnityCamera;
            if (unityCamera == null)
                throw new InvalidOperationException(
                    "The assigned streaming camera has no Unity camera.");

            _nativeCamera.Transform =
                unityCamera.transform.worldToLocalMatrix.ToZFlippedMatrix4();
            _nativeCamera.Position = StreamingCamera.GlobalPosition;
        }

        private void EnsureReady()
        {
            if (!IsReady)
                throw new InvalidOperationException(
                    "Both native and Unity streaming cameras must be assigned " +
                    "before updating the camera.");
        }
    }
}
