using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.MapSessions
{
    internal readonly struct MapLoadResult
    {
        private MapLoadResult(bool success, Node rootNode)
        {
            Success = success;
            RootNode = rootNode;
        }

        public bool Success { get; }
        public Node RootNode { get; }

        public static MapLoadResult Loaded(Node rootNode) =>
            new MapLoadResult(true, rootNode);

        public static MapLoadResult Failed() =>
            new MapLoadResult(false, null);
    }
}
