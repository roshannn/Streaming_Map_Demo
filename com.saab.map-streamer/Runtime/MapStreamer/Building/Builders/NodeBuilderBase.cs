using GizmoSDK.Gizmo3D;

using Saab.Foundation.Unity.MapStreamer.Nodes.Pooling;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
    public abstract class NodeBuilderBase :
        MonoBehaviour,
        INodeBuilder,
        IPooledNodeObjectPolicy
    {
        [SerializeField]
        private BuildPriority _mode = BuildPriority.Immediate;

        protected TextureManager _textureManager;
        protected MaterialManager _materialManager;

        public abstract PoolObjectFeature Feature { get; }

        public BuildPriority Priority => _mode;

        public abstract bool Build(
            NodeHandle nodeHandle,
            NodeHandle activeStateNode);

        void IPooledNodeObjectPolicy.Reset(
            GameObject gameObject,
            bool sharedAsset) =>
            BuiltObjectReturnedToPool(gameObject, sharedAsset);

        void IPooledNodeObjectPolicy.Initialize(GameObject gameObject) =>
            InitPoolObject(gameObject);

        public abstract void BuiltObjectReturnedToPool(
            GameObject gameObject,
            bool sharedAsset);

        public abstract void InitPoolObject(GameObject gameObject);

        public abstract bool CanBuild(
            Node node,
            TraversalState traversalState,
            IntersectMaskValue intersectMask);

        public void SetTextureManager(TextureManager textureManager)
        {
            _textureManager = textureManager;
        }

        public void SetMaterialManager(MaterialManager materialManager)
        {
            _materialManager = materialManager;
        }

        public virtual void Reset()
        {
        }
    }
}
