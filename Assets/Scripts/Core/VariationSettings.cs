using System;

namespace Roloc.Core
{
    [Serializable]
    public sealed class VariationSettings
    {
        public int ColorShiftStartScore = 20;
        public int OrbitStartScore = 40;
        public int DualOrbitStartScore = 60;
        public float PuckOrbitDegreesPerSecond = 18f;
        public float RingOrbitDegreesPerSecond = 12f;
    }
}
