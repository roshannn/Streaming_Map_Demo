using System;

namespace Saab.Foundation.Unity.MapStreamer.Runtime
{
    internal interface IPostTraversalEvents
    {
        event Action<bool> OnPostTraverse;
    }
}
