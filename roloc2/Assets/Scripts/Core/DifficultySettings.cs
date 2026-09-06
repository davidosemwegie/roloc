using UnityEngine;

namespace Roloc.Core
{
    [CreateAssetMenu(fileName = "Difficulty", menuName = "ROLOC/Difficulty Settings")]
    public sealed class DifficultySettings : ScriptableObject
    {
        [Header("Seconds per match")]
        [SerializeField, Min(0.1f)] private float initialSeconds = 3f;
        [SerializeField, Min(1)] private int firstThreshold = 5;
        [SerializeField, Min(0.1f)] private float firstSeconds = 2.5f;
        [SerializeField, Min(1)] private int secondThreshold = 20;
        [SerializeField, Min(0.1f)] private float secondSeconds = 2f;
        [SerializeField, Min(1)] private int thirdThreshold = 40;
        [SerializeField, Min(0.1f)] private float thirdSeconds = 1.75f;

        [Header("Shuffle rings above these scores")]
        [SerializeField, Min(0)] private int everyFiveAbove = 30;
        [SerializeField, Min(0)] private int everyTwoAbove = 60;
        [SerializeField, Min(0)] private int everyMatchAbove = 80;

        [Header("Shuffle middle pucks")]
        [SerializeField, Min(0)] private int puckShuffleAbove = 40;
        [SerializeField, Min(1)] private int puckShuffleInterval = 5;

        [Min(0)] public float TransitionSeconds = 0.24f;

        [Header("Random flow")]
        public bool RandomFlowEnabled = true;
        public FlowSettings Flow = new FlowSettings();
        [Range(0, 1)] public float FlowTransitionSeconds = .45f;
        [Range(0, 1.5f)] public float RotationSeconds = .75f;
        [Range(0, 5)] public float PuckFloatAmplitude = 3f;
        [Range(0, 10)] public float RingDriftRadius = 8f;

        public static float SecondsForScore(int score) => DefaultRules.SecondsForScore(score);
        public static bool ShouldShuffle(int score) => DefaultRules.ShouldShuffle(score);

        public float GetSeconds(int score)
        {
            if (score >= thirdThreshold) return thirdSeconds;
            if (score >= secondThreshold) return secondSeconds;
            if (score >= firstThreshold) return firstSeconds;
            return initialSeconds;
        }

        public bool IsShuffleScore(int score)
        {
            return score > everyMatchAbove || (score > everyTwoAbove && score % 2 == 0)
                || (score > everyFiveAbove && score % 5 == 0);
        }

        public bool IsPuckShuffleScore(int score) => score > puckShuffleAbove
            && score % Mathf.Max(1, puckShuffleInterval) == 0;
    }
}
