using System;

namespace Saab.Foundation.Unity.MapStreamer.Configuration
{
    [Flags]
    public enum MapStreamerOptions
    {
        None = 0,
        RenderInUpdate = 1 << 0,
        DisableInstancing = 1 << 1,
        LazyLoadAssets = 1 << 2,
    }
}
