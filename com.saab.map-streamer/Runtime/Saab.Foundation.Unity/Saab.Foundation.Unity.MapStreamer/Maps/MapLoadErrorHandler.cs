using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer
{
    public delegate void MapLoadErrorHandler(
        ref string url,
        string error,
        SerializeAdapter.AdapterError errorType,
        ref bool retry);
}
