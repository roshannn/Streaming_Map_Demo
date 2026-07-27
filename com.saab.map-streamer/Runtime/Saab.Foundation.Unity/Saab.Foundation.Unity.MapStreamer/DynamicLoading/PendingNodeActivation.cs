using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.DynamicLoading
{
    internal readonly struct PendingNodeActivation
    {
        public PendingNodeActivation(NodeActionEvent state, Node node)
        {
            State = state;
            Node = node;
        }

        public NodeActionEvent State { get; }
        public Node Node { get; }
    }
}
