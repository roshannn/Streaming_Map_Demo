using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules.Runtime
{
    internal sealed class TerrainModuleContextFactory
    {
        public bool TryCreate(
            GameObject gameObject,
            bool isAsset,
            out TerrainModuleContext context,
            out string failure)
        {
            context = default;

            if (gameObject == null)
            {
                failure = "The terrain GameObject is null.";
                return false;
            }

            if (!gameObject.TryGetComponent<NodeHandle>(out var nodeHandle))
            {
                failure = "The terrain has no NodeHandle.";
                return false;
            }

            if (!gameObject.TryGetComponent<MeshFilter>(out var meshFilter) ||
                meshFilter.sharedMesh == null)
            {
                failure = "The terrain has no mesh.";
                return false;
            }

            if (!gameObject.TryGetComponent<MeshRenderer>(out var renderer))
            {
                failure = "The terrain has no MeshRenderer.";
                return false;
            }

            var identity = new TerrainModuleIdentity(
                gameObject.GetInstanceID(),
                nodeHandle.allocationVersion);

            context = new TerrainModuleContext(
                identity,
                gameObject,
                nodeHandle,
                meshFilter.sharedMesh,
                renderer,
                nodeHandle.texture,
                nodeHandle.feature,
                nodeHandle.surfaceHeight,
                renderer.bounds,
                isAsset);
            failure = null;
            return true;
        }
    }
}
