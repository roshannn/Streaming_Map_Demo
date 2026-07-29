using System;

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.GizmoAdapter
{
    internal static class GizmoSdkRuntime
    {
        private static bool _basePlatformInitialized;
        private static bool _gizmo3DPlatformInitialized;
        private static bool _databaseInitialized;
        private static bool _configurationApplied;
        private static bool _bindingsActive;
        private static bool _sleepTimeoutChanged;
        private static int _previousSleepTimeout;

#if UNITY_ANDROID
        private static AndroidJavaObject _multicastLock;
#endif

        public static bool IsInitialized =>
            _basePlatformInitialized &&
            _gizmo3DPlatformInitialized &&
            _databaseInitialized &&
            _bindingsActive;

        public static bool EnsureInitialized()
        {
            if (IsInitialized)
                return true;

            var startedBasePlatform = false;
            var startedGizmo3DPlatform = false;
            try
            {
                if (!_basePlatformInitialized)
                {
                    if (!GizmoSDK.GizmoBase.Platform.Initialize())
                        return false;

                    _basePlatformInitialized = true;
                    startedBasePlatform = true;
                }

                if (!_gizmo3DPlatformInitialized)
                {
                    if (!GizmoSDK.Gizmo3D.Platform.Initialize())
                    {
                        RollBackInitialization(
                            startedBasePlatform,
                            startedGizmo3DPlatform);
                        return false;
                    }

                    _gizmo3DPlatformInitialized = true;
                    startedGizmo3DPlatform = true;
                }

                if (!_databaseInitialized)
                {
                    DbManager.Initialize();
                    _databaseInitialized = true;
                }

                if (!_configurationApplied)
                {
                    ApplyConfiguration();
                    _configurationApplied = true;
                }

                ActivateBindings();
                return true;
            }
            catch
            {
                DeactivateBindings();
                RollBackInitialization(
                    startedBasePlatform,
                    startedGizmo3DPlatform);
                throw;
            }
        }

        public static void ShutdownApplication()
        {
            try
            {
                DeactivateBindings();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (_gizmo3DPlatformInitialized)
            {
                try
                {
                    if (GizmoSDK.Gizmo3D.Platform.Uninitialize())
                    {
                        _gizmo3DPlatformInitialized = false;
                        _databaseInitialized = false;
                    }
                    else
                    {
                        Debug.LogWarning(
                            "Gizmo3D platform did not uninitialize.");
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (_basePlatformInitialized &&
                !_gizmo3DPlatformInitialized)
            {
                try
                {
                    if (GizmoSDK.GizmoBase.Platform.Uninitialize())
                    {
                        _basePlatformInitialized = false;
                        _configurationApplied = false;
                    }
                    else
                    {
                        Debug.LogWarning(
                            "GizmoBase platform did not uninitialize.");
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void ApplyConfiguration()
        {
#if UNITY_ANDROID
            SetupJavaBindings();
            Monitor.InstallMonitor("udp::45454?nic=${wlan0}");
            KeyDatabase.SetDefaultRegistry(
                $"/data/data/{Application.identifier}/files/gizmosdk.reg");
#else
            Monitor.InstallMonitor();
#endif

            Message.SetMessageLevel(MessageLevel.PERF_DEBUG);
            KeyDatabase.SetLocalRegistry("config.xml");
        }

        private static void ActivateBindings()
        {
            if (_bindingsActive)
                return;

            Message.OnMessage += RouteMessage;
            _bindingsActive = true;

            try
            {
                if (!_sleepTimeoutChanged)
                {
                    _previousSleepTimeout = Screen.sleepTimeout;
                    Screen.sleepTimeout = SleepTimeout.NeverSleep;
                    _sleepTimeoutChanged = true;
                }

#if UNITY_ANDROID
                AcquireMulticastLock();
#endif
            }
            catch
            {
                DeactivateBindings();
                throw;
            }
        }

        private static void DeactivateBindings()
        {
            if (_bindingsActive)
            {
                try
                {
                    Message.OnMessage -= RouteMessage;
                }
                finally
                {
                    _bindingsActive = false;
                }
            }

            try
            {
#if UNITY_ANDROID
                if (_multicastLock != null)
                {
                    try
                    {
                        _multicastLock.Call("release");
                    }
                    finally
                    {
                        var multicastLock = _multicastLock;
                        _multicastLock = null;
                        multicastLock.Dispose();
                    }
                }
#endif
            }
            finally
            {
                if (_sleepTimeoutChanged)
                {
                    _sleepTimeoutChanged = false;
                    Screen.sleepTimeout = _previousSleepTimeout;
                }
            }
        }

        private static void RollBackInitialization(
            bool startedBasePlatform,
            bool startedGizmo3DPlatform)
        {
            if (startedGizmo3DPlatform &&
                _gizmo3DPlatformInitialized)
            {
                try
                {
                    if (GizmoSDK.Gizmo3D.Platform.Uninitialize())
                    {
                        _gizmo3DPlatformInitialized = false;
                        _databaseInitialized = false;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (startedBasePlatform &&
                _basePlatformInitialized &&
                !_gizmo3DPlatformInitialized)
            {
                try
                {
                    if (GizmoSDK.GizmoBase.Platform.Uninitialize())
                    {
                        _basePlatformInitialized = false;
                        _configurationApplied = false;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

#if UNITY_ANDROID
        private static void SetupJavaBindings()
        {
            using (var playerClass =
                   new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity =
                   playerClass.GetStatic<AndroidJavaObject>(
                       "currentActivity"))
            using (var assetManager =
                   activity?.Call<AndroidJavaObject>("getAssets"))
            {
                if (assetManager != null)
                {
                    SerializeAdapter.SetAssetManagerHandle(
                        IntPtr.Zero,
                        assetManager.GetRawObject());
                }
            }
        }

        private static void AcquireMulticastLock()
        {
            if (_multicastLock != null)
                return;

            using (var playerClass =
                   new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity =
                   playerClass.GetStatic<AndroidJavaObject>(
                       "currentActivity"))
            using (var wifiManager =
                   activity?.Call<AndroidJavaObject>(
                       "getSystemService",
                       "wifi"))
            {
                _multicastLock =
                    wifiManager?.Call<AndroidJavaObject>(
                        "createMulticastLock",
                        "MapStreamer");
                _multicastLock?.Call("setReferenceCounted", false);
                _multicastLock?.Call("acquire");
            }
        }
#endif

        private static void RouteMessage(
            string sender,
            MessageLevel level,
            string message)
        {
            switch (level & MessageLevel.LEVEL_MASK)
            {
                case MessageLevel.MEM_DEBUG:
                case MessageLevel.PERF_DEBUG:
                case MessageLevel.DEBUG:
                case MessageLevel.TRACE_DEBUG:
                case MessageLevel.NOTICE:
                case MessageLevel.ALWAYS:
                    Debug.LogFormat(
                        LogType.Log,
                        LogOption.NoStacktrace,
                        null,
                        "{0}",
                        message);
                    break;
                case MessageLevel.WARNING:
                    Debug.LogWarning(message);
                    break;
                case MessageLevel.FATAL:
                    Debug.LogError(message);
                    break;
                case MessageLevel.ASSERT:
                    Debug.LogAssertion(message);
                    break;
            }
        }
    }
}
