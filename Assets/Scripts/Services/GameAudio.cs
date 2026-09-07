using UnityEngine;

namespace Roloc.Services
{
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        private AudioSource musicSource;
        private AudioSource matchSource;
        private AudioSource gameOverSource;
        private bool musicStarted;
        private bool musicPaused;
        private bool matchEnabled = true;
        private bool gameOverEnabled = true;

        public void Initialize(AudioClip music, AudioClip match, AudioClip gameOver, PlayerProgress prefs)
        {
            EnsureSources();
            StopMusic();
            matchSource.Stop();
            gameOverSource.Stop();
            musicSource.clip = music;
            matchSource.clip = match;
            gameOverSource.clip = gameOver;
            ApplyPreferences(prefs);
        }

        public void ApplyPreferences(PlayerProgress prefs)
        {
            EnsureSources();
            // Muting is separate from transport: unmuting a paused run cannot resume it.
            musicSource.mute = prefs != null && !prefs.MusicEnabled;
            matchEnabled = prefs == null || prefs.MatchEnabled;
            gameOverEnabled = prefs == null || prefs.GameOverEnabled;
            if (!matchEnabled) matchSource.Stop();
            if (!gameOverEnabled) gameOverSource.Stop();
        }

        public void StartMusic()
        {
            EnsureSources();
            musicSource.Stop();
            matchSource.Stop();
            gameOverSource.Stop();
            musicStarted = musicSource.clip != null;
            musicPaused = false;
            if (musicStarted) musicSource.Play();
        }

        public void PauseMusic()
        {
            if (!musicStarted || musicPaused || musicSource == null) return;
            musicPaused = true;
            musicSource.Pause();
        }

        public void ResumeMusic()
        {
            if (!musicStarted || !musicPaused || musicSource == null) return;
            musicPaused = false;
            musicSource.UnPause();
        }

        public void StopMusic()
        {
            musicStarted = false;
            musicPaused = false;
            if (musicSource != null) musicSource.Stop();
        }

        public void PlayMatch(int combo = 0, bool perfect = false)
        {
            EnsureSources();
            if (matchEnabled && matchSource.clip != null) matchSource.PlayOneShot(matchSource.clip, Mathf.Clamp(.7f + combo * .01f + (perfect ? .12f : 0), .7f, 1));
        }

        public void PlayGameOver()
        {
            EnsureSources();
            matchSource.Stop();
            if (gameOverEnabled && gameOverSource.clip != null)
                gameOverSource.PlayOneShot(gameOverSource.clip);
        }

        private void EnsureSources()
        {
            if (musicSource == null) musicSource = CreateSource(true);
            if (matchSource == null) matchSource = CreateSource(false);
            if (gameOverSource == null) gameOverSource = CreateSource(false);
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private void OnDisable()
        {
            StopMusic();
            if (matchSource != null) matchSource.Stop();
            if (gameOverSource != null) gameOverSource.Stop();
        }
    }
}
