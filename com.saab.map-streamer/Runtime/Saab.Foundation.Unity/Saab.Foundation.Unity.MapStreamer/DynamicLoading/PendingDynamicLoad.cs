using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.DynamicLoading
{
    internal readonly struct PendingDynamicLoad
    {
        public PendingDynamicLoad(
            DynamicLoadingState state,
            DynamicLoader loader,
            Node node)
        {
            State = state;
            Loader = loader;
            Node = node;
        }

        public DynamicLoadingState State { get; }
        public DynamicLoader Loader { get; }
        public Node Node { get; }
    }
}
