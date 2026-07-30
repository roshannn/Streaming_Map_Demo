using System;
using System.Collections.Generic;

using GizmoSDK.GizmoBase;

using Saab.Foundation.Unity.MapStreamer.Nodes.Pooling;
using Saab.Foundation.Unity.MapStreamer.Streaming;

namespace Saab.Foundation.Unity.MapStreamer.Building.Coordination
{
    internal sealed class BuilderLifecycleController : IBuilderRuntime
    {
        private readonly NodeBuilderBase[] _defaultBuilders;
        private readonly GeometryBuilderRegistry _builders;
        private readonly PooledNodeObjectPolicyRegistry _policies;
        private readonly TextureManager _textureManager;
        private readonly MaterialManager _materialManager;
        private readonly NodeHandlePool _nodeHandlePool;
        private readonly HashSet<INodeBuilder> _registeredBuilders =
            new HashSet<INodeBuilder>();

        private bool _hasInitialized;
        public bool SupportsInstancing { get; private set; }

        public BuilderLifecycleController(
            NodeBuilderBase[] defaultBuilders,
            GeometryBuilderRegistry builders,
            PooledNodeObjectPolicyRegistry policies,
            TextureManager textureManager,
            MaterialManager materialManager,
            NodeHandlePool nodeHandlePool)
        {
            _defaultBuilders = defaultBuilders ?? Array.Empty<NodeBuilderBase>();
            _builders = builders;
            _policies = policies;
            _textureManager = textureManager;
            _materialManager = materialManager;
            _nodeHandlePool = nodeHandlePool;
        }

        public void Initialize()
        {
            if (_hasInitialized)
                return;

            foreach (var builder in _defaultBuilders)
                Register(builder);

            if (_builders.Count == 0)
            {
                Message.Send(
                    "BuilderLifecycleController",
                    MessageLevel.WARNING,
                    "No node builder is registered.");
            }

            foreach (var builder in _builders)
            {
                builder.SetTextureManager(_textureManager);
                builder.SetMaterialManager(_materialManager);
            }

            _nodeHandlePool.Initialize(_policies);
            SupportsInstancing =
                _nodeHandlePool.HasPool(PoolObjectFeature.StaticMesh);

            _hasInitialized = true;
        }

        public void AddBuilder(INodeBuilder builder)
        {
            EnsureMutable();
            Register(builder);
        }

        public void RemoveBuilder(INodeBuilder builder)
        {
            EnsureMutable();
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            _registeredBuilders.Remove(builder);
            _builders.Remove(builder);
            if (builder is IPooledNodeObjectPolicy policy)
                _policies.Remove(policy);
        }

        private void Register(INodeBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (!_registeredBuilders.Add(builder))
                return;

            if (!(builder is IPooledNodeObjectPolicy policy))
            {
                _registeredBuilders.Remove(builder);
                throw new ArgumentException(
                    $"{builder.GetType().Name} must provide a pooled-object " +
                    $"policy for feature {builder.Feature}.",
                    nameof(builder));
            }

            _builders.Add(builder);
            _policies.Add(policy);
        }

        private void EnsureMutable()
        {
            if (_hasInitialized)
            {
                throw new InvalidOperationException(
                    "Builders cannot be added or removed after initialization.");
            }
        }
    }
}
