using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
    internal static class SDKInitializer
    {
        private static bool _sdkInitialized;
        private static bool _bindingsActive;
        private static bool _dynamicLoadersRunning;

#if UNITY_ANDROID
        private static AndroidJavaObject _multicastLock;
#endif

        public static bool Initialize()
        {
            if (_bindingsActive)
                return true;

            if (!_sdkInitialized)
            {
                if (!GizmoSDK.GizmoBase.Platform.Initialize())
                    return false;
                if (!GizmoSDK.Gizmo3D.Platform.Initialize())
                    return false;

                DbManager.Initialize();

#if UNITY_ANDROID
                Monitor.InstallMonitor("udp::45454?nic=${wlan0}");
                KeyDatabase.SetDefaultRegistry(
                    $"/data/data/{Application.identifier}/files/gizmosdk.reg");
                SetupJavaBindings();
#else
                Monitor.InstallMonitor();
#endif

                Message.SetMessageLevel(MessageLevel.PERF_DEBUG);
                KeyDatabase.SetLocalRegistry("config.xml");
                DynamicLoader.UsePreCache(true);
                _sdkInitialized = true;
            }

            Message.OnMessage += RouteMessage;
            _bindingsActive = true;

            try
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if UNITY_ANDROID
                AcquireMulticastLock();
#endif

                return true;
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        public static void StartDynamicLoaders(byte loaderCount)
        {
            DynamicLoaderManager.SetNumberOfActiveLoaders(loaderCount);
            if (_dynamicLoadersRunning)
                return;

            DynamicLoaderManager.StartManager();
            _dynamicLoadersRunning = true;
        }

        public static void StopDynamicLoaders()
        {
            if (!_dynamicLoadersRunning)
                return;

            DynamicLoaderManager.StopManager();
            _dynamicLoadersRunning = false;
        }

        public static void Shutdown()
        {
            if (!_bindingsActive)
                return;

            StopDynamicLoaders();
            Message.OnMessage -= RouteMessage;

#if UNITY_ANDROID
            if (_multicastLock != null)
            {
                _multicastLock.Call("release");
                _multicastLock.Dispose();
                _multicastLock = null;
            }
#endif

            _bindingsActive = false;
        }

#if UNITY_ANDROID
        private static void SetupJavaBindings()
        {
            using (var playerClass =
                   new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity =
                   playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var assetManager =
                   activity?.Call<AndroidJavaObject>("getAssets"))
            {
                if (assetManager != null)
                {
                    SerializeAdapter.SetAssetManagerHandle(
                        System.IntPtr.Zero,
                        assetManager.GetRawObject());
                }
            }
        }

        private static void AcquireMulticastLock()
        {
            using (var playerClass =
                   new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity =
                   playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var wifiManager =
                   activity?.Call<AndroidJavaObject>(
                       "getSystemService",
                       "wifi"))
            {
                _multicastLock = wifiManager?.Call<AndroidJavaObject>(
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
