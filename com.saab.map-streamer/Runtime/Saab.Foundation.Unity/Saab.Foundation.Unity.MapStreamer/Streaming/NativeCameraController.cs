using System;

using GizmoSDK.Gizmo3D;

using Saab.Unity.Extensions;

using UnityEngine;

using NativeCamera = GizmoSDK.Gizmo3D.Camera;

namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    internal sealed class NativeCameraController
    {
        public NativeCamera NativeCamera { get; private set; }
        public IStreamingCamera StreamingCamera { get; private set; }
        public bool IsReady => NativeCamera != null && StreamingCamera != null;

        public void SetNativeCamera(NativeCamera nativeCamera)
        {
            NativeCamera = nativeCamera ??
                throw new ArgumentNullException(nameof(nativeCamera));
            Debug.Log("Native camera assigned.");
            SynchronizeInitialStateIfReady();
        }

        public void SetUnityCamera(IStreamingCamera streamingCamera)
        {
            StreamingCamera = streamingCamera ??
                throw new ArgumentNullException(nameof(streamingCamera));
            Debug.Log(
                $"Unity camera assigned: " +
                $"{streamingCamera.UnityCamera?.name ?? "<missing>"}.");
            SynchronizeInitialStateIfReady();
        }

        public void ClearNativeCamera()
        {
            NativeCamera = null;
            Debug.Log("Native camera reference cleared.");
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
            {
                Debug.Log(
                    "Initial camera synchronization deferred until both " +
                    "cameras are assigned.");
                return;
            }

            var unityCamera = StreamingCamera.UnityCamera;
            if (unityCamera == null)
                throw new InvalidOperationException(
                    "The assigned streaming camera has no Unity camera.");

            if (NativeCamera is PerspCamera perspectiveCamera)
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
            Debug.Log(
                $"Initial camera state synchronized from {unityCamera.name}.");
        }

        private void SynchronizePose()
        {
            var unityCamera = StreamingCamera.UnityCamera;
            if (unityCamera == null)
                throw new InvalidOperationException(
                    "The assigned streaming camera has no Unity camera.");

            NativeCamera.Transform =
                unityCamera.transform.worldToLocalMatrix.ToZFlippedMatrix4();
            NativeCamera.Position = StreamingCamera.GlobalPosition;
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
