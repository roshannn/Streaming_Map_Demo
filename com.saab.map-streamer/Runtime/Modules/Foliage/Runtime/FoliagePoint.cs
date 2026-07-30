using System.Runtime.InteropServices;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FoliagePoint
    {
        public Vector3 Position;
        public uint Color;
        public uint up;
        public uint right;
        public short Height;
        public short Random;
        public short Visibility;
        public short Pad0;
    }
}
