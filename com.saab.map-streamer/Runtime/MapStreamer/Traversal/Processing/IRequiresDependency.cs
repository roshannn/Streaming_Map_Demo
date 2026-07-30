// Copyright 2021 saab AB

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processing
{
    internal interface IRequiresDependency<TDependency>
    {
        void Inject(TDependency dependency);
    }
}
