namespace Roloc.Core
{
    public static class ReviveRules
    {
        public const int MaxBank = 3;

        public static bool IsMilestone(int score)
            => score == 20 || score == 50 || (score >= 100 && score % 50 == 0);
    }
}
