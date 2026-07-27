using Saab.Foundation.Unity.MapStreamer.Traversal.Processors;
using Saab.Utility.Unity.NodeUtils;

using unTransform = UnityEngine.Transform;

namespace Saab.Foundation.Unity.MapStreamer.NodeProcessing
{
    internal sealed class NodeHierarchyUnloader
    {
        private readonly INodeUpdateRegistry _updates;

        public NodeHierarchyUnloader(INodeUpdateRegistry updates)
        {
            _updates = updates;
        }

        public void Unload(unTransform transform)
        {
            if (transform.TryGetComponent<NodeHandle>(out var handle))
            {
                _updates.Unregister(handle);

                if (handle.inNodeUtilsRegistry)
                {
                    NodeUtils.RemoveGameObjectReferenceUnsafe(
                        handle.node.GetNativeReference(),
                        transform.gameObject);
                }

                // Invalidates delayed builds targeting this pooled handle.
                ++handle.version;
            }

            for (var i = 0; i < transform.childCount; ++i)
                Unload(transform.GetChild(i));
        }
    }
}
