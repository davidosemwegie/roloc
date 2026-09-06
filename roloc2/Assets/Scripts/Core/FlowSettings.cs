using System;

namespace Roloc.Core
{
    [Serializable]
    public sealed class FlowSettings
    {
        public int StartScore = 5;
        public int DriftStartScore = 20;
        public int RotationStartScore = 40;
        public int MinMatches = 3;
        public int MaxMatches = 5;
        public int MinCalmMatches = 1;
        public int MaxCalmMatches = 2;
        public float BreatherExtraSeconds = 0.65f;
        public int BreatherCooldownMatches = 10;
    }
}
