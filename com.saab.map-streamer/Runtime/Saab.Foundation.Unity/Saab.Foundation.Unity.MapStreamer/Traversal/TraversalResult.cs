// Copyright 2021 saab AB

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal readonly struct TraversalResult
    {
        private TraversalResult(TraversalOutcome outcome, GameObject gameObject)
        {
            Outcome = outcome;
            GameObject = gameObject;
        }

        public TraversalOutcome Outcome { get; }
        public GameObject GameObject { get; }
        public bool HasGameObject => GameObject != null;

        public static TraversalResult Created(GameObject gameObject)
        {
            return new TraversalResult(TraversalOutcome.Created, gameObject);
        }

        public static TraversalResult Handled()
        {
            return new TraversalResult(TraversalOutcome.Handled, null);
        }

        public static TraversalResult Filtered()
        {
            return new TraversalResult(TraversalOutcome.Filtered, null);
        }

        public static TraversalResult Deferred()
        {
            return new TraversalResult(TraversalOutcome.Deferred, null);
        }

        public static TraversalResult Skipped()
        {
            return new TraversalResult(TraversalOutcome.Skipped, null);
        }
    }
}
