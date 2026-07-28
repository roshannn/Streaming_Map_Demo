using System;

namespace Saab.Foundation.Unity.MapStreamer
{
    internal interface IPostTraversalEvents
    {
        event Action<bool> OnPostTraverse;
    }
}
