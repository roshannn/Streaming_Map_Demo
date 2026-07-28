// Copyright 2021 saab AB

using System;
using System.Collections.Generic;
using GizmoSDK.Gizmo3D;
using Saab.Foundation.Unity.MapStreamer.NodeProcessing;
using Saab.Foundation.Unity.MapStreamer.Building.Builders;
using Saab.Foundation.Unity.MapStreamer.Nodes.Processing;
using Saab.Foundation.Unity.MapStreamer.Traversal;
using Saab.Foundation.Unity.MapStreamer.Streaming;

namespace Saab.Foundation.Unity.MapStreamer.Building.Coordination
{
    internal sealed class NodeBuildCoordinator : IBuildScheduler
    {
        private readonly Queue<PendingNodeBuild> _pending =
            new Queue<PendingNodeBuild>(1000);

        public void Build(INodeBuilder builder, in TraversalContext context)
        {
            switch (builder.Priority)
            {
                case BuildPriority.Immediate:
                    var target = context.Node.BuildTarget;
                    var activeTarget =
                        context.ActiveStateNode.IsValid
                            ? context.ActiveStateNode.BuildTarget
                            : default;
                    target.Build(builder, activeTarget);
                    break;
                case BuildPriority.Low:
                    _pending.Enqueue(new PendingNodeBuild(
                        builder,
                        context.Node,
                        context.ActiveStateNode));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public void Process(TimeSpan maxBuildTime)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            while (_pending.Count > 0 && timer.Elapsed < maxBuildTime)
            {
                var build = _pending.Dequeue();
                var target = build.Target;

                if (build.Version != target.Version)
                    continue;

                var activeTarget = build.ActiveStateTarget;
                if (activeTarget.IsValid &&
                    !activeTarget.HasNativeNode)
                    continue;

                target.Build(build.Builder, activeTarget);
            }
        }

        public void RegisterAssetPrefab(
            AssetInstanceBuilder assetInstances,
            Geometry geometry,
            TraversalNode node)
        {
            node.BuildTarget.RegisterAssetPrefab(
                geometry,
                assetInstances);
        }

        public void Clear() => _pending.Clear();
    }
}
