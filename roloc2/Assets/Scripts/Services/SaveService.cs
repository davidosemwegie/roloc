using System;
using System.IO;
using UnityEngine;

namespace Roloc.Services
{
    /// <summary>Small local-only save; no dependency on the previous game's storage.</summary>
    public sealed class SaveService
    {
        public const string FileName = "roloc2-progress.json";
        private readonly string directory;
        public PlayerProgress Data { get; private set; }

        public SaveService(string directory = null)
        {
            this.directory = directory ?? Application.persistentDataPath;
            Load();
        }

        public void Load()
        {
            Data = new PlayerProgress();
            try
            {
                string path = Path.Combine(directory, FileName);
                if (!File.Exists(path)) return;
                string json = File.ReadAllText(path).Trim();
                if (!json.StartsWith("{") || !json.EndsWith("}"))
                    throw new FormatException("Save must be a JSON object.");
                // Overwrite initialized defaults so newly added preferences remain enabled.
                var loaded = new PlayerProgress();
                JsonUtility.FromJsonOverwrite(json, loaded);
                Sanitize(loaded);
                Data = loaded;
            }
            catch (Exception exception)
            {
                // Leave an unreadable save untouched until the next explicit save.
                Debug.LogWarning($"ROLOC could not load progress: {exception.Message}");
            }
        }

        public void Save()
        {
            string temporaryPath = null;
            try
            {
                Sanitize(Data);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, FileName);
                temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(Data));
                // Both files reside on the same filesystem; replace the completed write atomically.
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ROLOC could not save progress: {exception.Message}");
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                    catch (Exception) { /* Keep the game usable if storage is unavailable. */ }
                }
            }
        }

        /// <summary>Call once on a completed run, never for an abandoned run.</summary>
        public void RecordGame(int score)
        {
            Sanitize(Data);
            score = Math.Max(0, score);
            Data.HighScore = Math.Max(Data.HighScore, score);
            if (Data.GamesPlayed < int.MaxValue) Data.GamesPlayed++;
            Data.TotalScore = Data.TotalScore > long.MaxValue - score
                ? long.MaxValue : Data.TotalScore + score;
            Save();
        }

        public void CompleteTutorial()
        {
            Data.TutorialCompleted = true;
            Save();
        }

        private static void Sanitize(PlayerProgress data)
        {
            data.HighScore = Math.Max(0, data.HighScore);
            data.GamesPlayed = Math.Max(0, data.GamesPlayed);
            data.TotalScore = Math.Max(0L, data.TotalScore);
        }
    }
}
