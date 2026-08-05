using StreamingMapDemo.Simulation;
using UnityEngine;

namespace StreamingMapDemo.Drones
{
    public interface IWorldOrigin
    {
        Vector3 ToLocal(GlobalPosition globalPosition);
        GlobalPosition ToGlobal(Vector3 localPosition);
    }

    public sealed class UnityWorldOrigin : IWorldOrigin
    {
        public GlobalPosition Origin { get; set; }
        public UnityWorldOrigin(GlobalPosition origin) => Origin = origin;
        public Vector3 ToLocal(GlobalPosition position)
        {
            Float3 value = GlobalPosition.UnityDelta(Origin, position);
            return new Vector3(value.X, value.Y, value.Z);
        }
        public GlobalPosition ToGlobal(Vector3 local) => Origin.AddUnityDisplacement(local.ToFloat3());
    }

    internal static class SimulationConversions
    {
        public static Float3 ToFloat3(this Vector3 value) => new Float3(value.x, value.y, value.z);
        public static Vector3 ToVector3(this Float3 value) => new Vector3(value.X, value.Y, value.Z);
    }
}
