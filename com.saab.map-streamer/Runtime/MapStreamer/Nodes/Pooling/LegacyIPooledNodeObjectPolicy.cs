using System;

namespace Saab.Foundation.Unity.MapStreamer.NodeProcessing
{
    /// <summary>
    /// Compatibility alias for the corrected pooling namespace.
    /// </summary>
    [Obsolete(
        "Use Saab.Foundation.Unity.MapStreamer.Nodes.Pooling." +
        "IPooledNodeObjectPolicy.")]
    public interface IPooledNodeObjectPolicy :
        Nodes.Pooling.IPooledNodeObjectPolicy
    {
    }
}
