using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.Streaming;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.GizmoAdapter
{
    public interface IGizmoMapCallbacks
    {
        GameObject Install(string url, Node node);
    }

    public sealed class GizmoMapDataSource : IMapDataSource
    {
        private const string MessageSource =
            "Saab.Foundation.Unity.MapStreamer.GizmoMapDataSource";

        public bool TryLoad(
            string url,
            out INativeMapHandle map,
            out MapLoadFailure failure)
        {
            var error = string.Empty;
            var errorType = SerializeAdapter.AdapterError.NO_ERROR;
            var node = DbManager.LoadDB(url, ref error, ref errorType);
            if (node != null && node.IsValid())
            {
                map = new GizmoMapHandle(node);
                failure = default;
                return true;
            }

            Message.Send(
                MessageSource,
                MessageLevel.WARNING,
                $"Failed to load map {url}");
            node?.ReleaseNoDelete();
            map = null;
            failure = new MapLoadFailure(error, (int)errorType);
            return false;
        }
    }

    public sealed class GizmoMapInstaller : IMapInstaller
    {
        private readonly IGizmoMapCallbacks _callbacks;

        public GizmoMapInstaller(IGizmoMapCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        public GameObject Install(string url, INativeMapHandle map)
        {
            if (map == null)
                return _callbacks.Install(url, null);
            if (!(map is GizmoMapHandle gizmoMap))
                throw new System.ArgumentException(
                    "Map handle was not created by the Gizmo adapter.",
                    nameof(map));

            var root = _callbacks.Install(url, gizmoMap.Node);
            gizmoMap.TransferOwnership();
            return root;
        }
    }

    internal sealed class GizmoMapHandle : INativeMapHandle
    {
        private Node _node;

        public GizmoMapHandle(Node node)
        {
            _node = node;
            Identity = node.GetNativeReference().ToInt64();
        }

        public long Identity { get; }

        public Node Node => _node;

        public void TransferOwnership()
        {
            _node = null;
        }

        public void Dispose()
        {
            _node?.ReleaseNoDelete();
            _node = null;
        }
    }
}
