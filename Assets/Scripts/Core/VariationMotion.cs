namespace Roloc.Core
{
    /// <summary>Compatible effects for each group. A group never floats/drifts and orbits at once.</summary>
    public static class VariationMotion
    {
        public static bool FloatsPucks(FlowMode mode) => mode == FlowMode.Floating
            || mode == FlowMode.FloatingDrifting || mode == FlowMode.RingOrbitFloating;
        public static bool DriftsRings(FlowMode mode) => mode == FlowMode.Drifting
            || mode == FlowMode.FloatingDrifting || mode == FlowMode.PuckOrbitDrifting;
        public static bool OrbitsPucks(FlowMode mode) => mode == FlowMode.PuckOrbit
            || mode == FlowMode.DualOrbit || mode == FlowMode.PuckOrbitDrifting;
        public static bool OrbitsRings(FlowMode mode) => mode == FlowMode.RingOrbit
            || mode == FlowMode.DualOrbit || mode == FlowMode.RingOrbitFloating;
        public static bool HasOrbit(FlowMode mode) => OrbitsPucks(mode) || OrbitsRings(mode);
    }
}
