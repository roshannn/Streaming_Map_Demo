using System;

namespace Saab.Foundation.Unity.MapStreamer
{
    [Flags]
    public enum TraversalState
    {
        None,
        Asset = 0x01,
        AssetInstance = 0x02,
    }
}
