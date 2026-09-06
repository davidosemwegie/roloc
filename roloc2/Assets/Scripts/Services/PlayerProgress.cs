using System;

namespace Roloc.Services
{
    [Serializable]
    public sealed class PlayerProgress
    {
        public int HighScore;
        public int GamesPlayed;
        public long TotalScore;
        public bool TutorialCompleted;
        public bool MusicEnabled = true;
        public bool MatchEnabled = true;
        public bool GameOverEnabled = true;
    }
}
