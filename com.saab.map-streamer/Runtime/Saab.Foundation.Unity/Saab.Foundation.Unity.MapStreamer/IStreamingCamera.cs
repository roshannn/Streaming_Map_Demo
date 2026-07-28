using GizmoSDK.GizmoBase;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
    public interface IStreamingCamera
    {
        Camera UnityCamera { get; }
        Vec3D GlobalPosition { get; }
        float LodFactor { get; }
        double Update(double renderTime);
    }
}
