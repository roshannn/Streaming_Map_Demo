// Copyright 2021 saab AB

namespace Saab.Foundation.Unity.MapStreamer.Traversal.Processors
{
    internal interface IRequiresDependency<TDependency>
    {
        void Inject(TDependency dependency);
    }
}
