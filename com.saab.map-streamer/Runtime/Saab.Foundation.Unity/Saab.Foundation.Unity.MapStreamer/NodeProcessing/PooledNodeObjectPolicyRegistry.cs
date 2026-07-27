// Copyright 2021 saab AB

using System.Collections;
using System.Collections.Generic;

namespace Saab.Foundation.Unity.MapStreamer.NodeProcessing
{
    internal sealed class PooledNodeObjectPolicyRegistry :
        IEnumerable<IPooledNodeObjectPolicy>
    {
        private readonly List<IPooledNodeObjectPolicy> _policies =
            new List<IPooledNodeObjectPolicy>();

        public void Add(IPooledNodeObjectPolicy policy) =>
            _policies.Add(policy);

        public void Remove(IPooledNodeObjectPolicy policy) =>
            _policies.Remove(policy);

        public IEnumerator<IPooledNodeObjectPolicy> GetEnumerator() =>
            _policies.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
