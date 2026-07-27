// Copyright 2021 saab AB

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal readonly struct TraversalResult
    {
        private TraversalResult(TraversalOutcome outcome, TraversalNode node)
        {
            Outcome = outcome;
            Node = node;
        }

        public TraversalOutcome Outcome { get; }
        public TraversalNode Node { get; }
        public GameObject GameObject =>
            Node.IsValid ? Node.GameObject : null;
        public bool HasGameObject => Node.IsValid;

        public static TraversalResult Created(TraversalNode node)
        {
            return new TraversalResult(TraversalOutcome.Created, node);
        }

        public static TraversalResult Handled()
        {
            return new TraversalResult(TraversalOutcome.Handled, default);
        }

        public static TraversalResult Filtered()
        {
            return new TraversalResult(TraversalOutcome.Filtered, default);
        }

        public static TraversalResult Deferred()
        {
            return new TraversalResult(TraversalOutcome.Deferred, default);
        }

        public static TraversalResult Skipped()
        {
            return new TraversalResult(TraversalOutcome.Skipped, default);
        }
    }
}
