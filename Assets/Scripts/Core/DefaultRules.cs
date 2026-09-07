namespace Roloc.Core
{
    public static class DefaultRules
    {
        public static float FlowSecondsForScore(int score)
        {
            if (score >= 50) return 2.25f;
            if (score >= 25) return 2.5f;
            if (score >= 10) return 3f;
            return 3.5f;
        }

        public static float SecondsForScore(int score)
        {
            if (score >= 40) return 1.75f;
            if (score >= 20) return 2f;
            if (score >= 5) return 2.5f;
            return 3f;
        }

        public static bool ShouldShuffle(int score)
        {
            return score > 80 || (score > 60 && score % 2 == 0) || (score > 30 && score % 5 == 0);
        }

        public static bool ShouldShufflePucks(int score) => score > 40 && score % 5 == 0;
    }
}
