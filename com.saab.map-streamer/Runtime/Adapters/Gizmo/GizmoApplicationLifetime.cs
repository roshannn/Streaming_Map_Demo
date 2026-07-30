using System;
using System.Collections.Generic;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.GizmoAdapter
{
    public static class GizmoApplicationLifetime
    {
        private static readonly object Sync = new object();
        private static readonly List<ShutdownRegistration>
            ShutdownRegistrations =
                new List<ShutdownRegistration>();

        private static bool _quitHookInstalled;
        private static bool _quitting;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Bootstrap()
        {
            lock (Sync)
            {
                _quitting = false;
                if (!_quitHookInstalled)
                {
                    Application.quitting += ShutdownApplication;
                    _quitHookInstalled = true;
                }
            }

            try
            {
                if (!GizmoSdkRuntime.EnsureInitialized())
                {
                    Debug.LogError(
                        "Gizmo SDK did not initialize during " +
                        "application bootstrap. The streaming backend " +
                        "will retry initialization before use.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Gizmo SDK application bootstrap failed. The " +
                    "streaming backend will retry initialization " +
                    "before use.");
                Debug.LogException(exception);
            }
        }

        public static IDisposable RegisterShutdown(Action shutdown)
        {
            if (shutdown == null)
                throw new ArgumentNullException(nameof(shutdown));

            lock (Sync)
            {
                if (_quitting)
                {
                    throw new InvalidOperationException(
                        "Cannot register application shutdown work " +
                        "while the application is quitting.");
                }

                var registration =
                    new ShutdownRegistration(shutdown);
                ShutdownRegistrations.Add(registration);
                return registration;
            }
        }

        private static void ShutdownApplication()
        {
            try
            {
                InvokeShutdownCallbacks();
            }
            finally
            {
                GizmoSdkRuntime.ShutdownApplication();
            }
        }

        internal static void InvokeShutdownCallbacks()
        {
            ShutdownRegistration[] registrations;
            lock (Sync)
            {
                if (_quitting)
                    return;

                _quitting = true;
                registrations = ShutdownRegistrations.ToArray();
                ShutdownRegistrations.Clear();
            }

            for (var index = registrations.Length - 1;
                 index >= 0;
                 index--)
            {
                var registration = registrations[index];
                if (registration.IsDisposed)
                    continue;

                try
                {
                    registration.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "An application shutdown callback failed.");
                    Debug.LogException(exception);
                }
            }
        }

        internal static void ResetCallbacksForTests()
        {
            lock (Sync)
            {
                ShutdownRegistrations.Clear();
                _quitting = false;
            }
        }

        private sealed class ShutdownRegistration : IDisposable
        {
            private Action _shutdown;

            public ShutdownRegistration(Action shutdown)
            {
                _shutdown = shutdown;
            }

            public bool IsDisposed => _shutdown == null;

            public void Invoke()
            {
                _shutdown?.Invoke();
            }

            public void Dispose()
            {
                lock (Sync)
                {
                    if (_shutdown == null)
                        return;

                    _shutdown = null;
                    ShutdownRegistrations.Remove(this);
                }
            }
        }
    }
}
