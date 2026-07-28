using GizmoSDK.Gizmo3D;

using NativeCamera = GizmoSDK.Gizmo3D.Camera;

namespace Saab.Foundation.Unity.MapStreamer.Streaming
{
    internal interface IStreamingRuntimeState
    {
        bool IsInitialized { get; }
        NativeCamera NativeCamera { get; }
        Context NativeContext { get; }
        CullTraverseAction TraverseAction { get; }
    }
}
