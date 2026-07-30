using System;

namespace Saab.Foundation.Unity.MapStreamer
{
    [Flags]
    public enum PoolObjectFeature : byte
    {
        None = 0,
        Terrain = 1 << 0,
        StaticMesh = 1 << 1,
        Crossboard = 1 << 2,
    }
}
