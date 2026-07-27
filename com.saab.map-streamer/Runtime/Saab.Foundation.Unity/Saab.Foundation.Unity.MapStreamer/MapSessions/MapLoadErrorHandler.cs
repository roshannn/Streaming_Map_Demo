using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer.MapSessions
{
    internal delegate void MapLoadErrorHandler(
        ref string url,
        string error,
        SerializeAdapter.AdapterError errorType,
        ref bool retry);
}
