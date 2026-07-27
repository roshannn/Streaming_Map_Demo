using System;
using System.Runtime.ExceptionServices;

using GizmoSDK.Gizmo3D;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.Streaming.Synchronization;

using gzCamera = GizmoSDK.Gizmo3D.Camera;

namespace Saab.Foundation.Unity.MapStreamer.Native
{
    internal readonly struct NativeRenderResources
    {
        public NativeRenderResources(
            gzCamera camera,
            Context context,
            CullTraverseAction traverseAction)
        {
            Camera = camera;
            Context = context;
            TraverseAction = traverseAction;
        }

        public gzCamera Camera { get; }
        public Context Context { get; }
        public CullTraverseAction TraverseAction { get; }
    }

    internal sealed class NativeSceneSession : IDisposable
    {
        private readonly IStreamingLock _streamingLock;

        private Scene _scene;
        private gzCamera _camera;
        private Context _context;
        private CullTraverseAction _traverseAction;
        private bool _debugEnabled;

        public NativeSceneSession(IStreamingLock streamingLock)
        {
            _streamingLock = streamingLock;
        }

        public bool IsInitialized { get; private set; }

        public Scene Scene
        {
            get
            {
                EnsureInitialized();
                return _scene;
            }
        }

        public NativeRenderResources RenderResources
        {
            get
            {
                EnsureInitialized();
                return new NativeRenderResources(
                    _camera,
                    _context,
                    _traverseAction);
            }
        }

        public void Initialize()
        {
            if (IsInitialized)
                return;

            using (_streamingLock.AcquireEdit())
            {
                if (IsInitialized)
                    return;

                PerspCamera camera = null;
                Scene scene = null;
                Context context = null;
                CullTraverseAction traverseAction = null;
                var debugEnabled = false;

                try
                {
                    camera = new PerspCamera("Test")
                    {
                        RoiPosition = true,
                    };

                    MapControl.SystemMap.Camera = camera;

                    scene = new Scene("Scene");
                    camera.Scene = scene;

                    context = new Context();

#if DEBUG_CAMERA
                    camera.Debug(context);
                    debugEnabled = true;
#endif

                    traverseAction = new CullTraverseAction();

                    _camera = camera;
                    _scene = scene;
                    _context = context;
                    _traverseAction = traverseAction;
                    _debugEnabled = debugEnabled;
                    IsInitialized = true;
                }
                catch
                {
                    Cleanup(
                        camera,
                        scene,
                        context,
                        traverseAction,
                        debugEnabled,
                        throwOnFailure: false);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (!IsInitialized)
                return;

            using (_streamingLock.AcquireEdit())
            {
                if (!IsInitialized)
                    return;

                var camera = _camera;
                var scene = _scene;
                var context = _context;
                var traverseAction = _traverseAction;
                var debugEnabled = _debugEnabled;

                _camera = null;
                _scene = null;
                _context = null;
                _traverseAction = null;
                _debugEnabled = false;
                IsInitialized = false;

                Cleanup(
                    camera,
                    scene,
                    context,
                    traverseAction,
                    debugEnabled,
                    throwOnFailure: true);
            }
        }

        private static void Cleanup(
            gzCamera camera,
            Scene scene,
            Context context,
            CullTraverseAction traverseAction,
            bool debugEnabled,
            bool throwOnFailure)
        {
            Exception firstException = null;

            if (debugEnabled && camera != null && context != null)
                TryCleanup(
                    () => camera.Debug(context, false),
                    ref firstException);

            TryCleanup(() => traverseAction?.Dispose(), ref firstException);
            TryCleanup(() => camera?.Dispose(), ref firstException);
            TryCleanup(() => context?.Dispose(), ref firstException);
            TryCleanup(() => scene?.Dispose(), ref firstException);

            if (firstException == null)
                return;

            if (throwOnFailure)
                ExceptionDispatchInfo.Capture(firstException).Throw();

            UnityEngine.Debug.LogException(firstException);
        }

        private static void TryCleanup(
            Action cleanup,
            ref Exception firstException)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                if (firstException == null)
                    firstException = exception;
                else
                    UnityEngine.Debug.LogException(exception);
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "The native scene session is not initialized.");
        }
    }
}
