using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Roloc.Services.Tests
{
    public sealed class SaveServiceTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "roloc2-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(directory, true);
        }

        [Test]
        public void MissingSaveStartsWithEnabledAudioAndNoProgress()
        {
            var data = new SaveService(directory).Data;
            Assert.That(data.HighScore, Is.Zero);
            Assert.That(data.GamesPlayed, Is.Zero);
            Assert.That(data.TotalScore, Is.Zero);
            Assert.That(data.TutorialCompleted, Is.False);
            Assert.That(data.MusicEnabled && data.MatchEnabled && data.GameOverEnabled, Is.True);
        }

        [Test]
        public void ProgressAndIndependentPreferencesSurviveRepeatedAtomicSaves()
        {
            var saves = new SaveService(directory);
            saves.RecordGame(12);
            saves.RecordGame(3);
            saves.CompleteTutorial();
            saves.Data.MusicEnabled = false;
            saves.Data.GameOverEnabled = false;
            saves.Save();
            var loaded = new SaveService(directory).Data;
            Assert.That(loaded.HighScore, Is.EqualTo(12));
            Assert.That(loaded.GamesPlayed, Is.EqualTo(2));
            Assert.That(loaded.TotalScore, Is.EqualTo(15L));
            Assert.That(loaded.TutorialCompleted, Is.True);
            Assert.That(loaded.MusicEnabled, Is.False);
            Assert.That(loaded.MatchEnabled, Is.True);
            Assert.That(loaded.GameOverEnabled, Is.False);
            Assert.That(File.Exists(Path.Combine(directory, SaveService.FileName) + ".tmp"), Is.False);
        }

        [Test]
        public void CorruptSaveFallsBackWithoutDeletingOriginal()
        {
            string path = Path.Combine(directory, SaveService.FileName);
            File.WriteAllText(path, "broken save");
            LogAssert.Expect(LogType.Warning, new Regex("ROLOC could not load progress:"));
            var saves = new SaveService(directory);
            Assert.That(saves.Data.GamesPlayed, Is.Zero);
            Assert.That(saves.Data.MusicEnabled, Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo("broken save"));
        }

        [Test]
        public void MissingFieldsKeepDefaultsAndNegativeStatsAreClamped()
        {
            File.WriteAllText(Path.Combine(directory, SaveService.FileName),
                "{\"HighScore\":-5,\"TotalScore\":-9,\"GamesPlayed\":-1}");
            var data = new SaveService(directory).Data;
            Assert.That(data.HighScore, Is.Zero);
            Assert.That(data.GamesPlayed, Is.Zero);
            Assert.That(data.TotalScore, Is.Zero);
            Assert.That(data.MusicEnabled && data.MatchEnabled && data.GameOverEnabled, Is.True);
        }

        [Test]
        public void AggregatesSaturateInsteadOfOverflowing()
        {
            var saves = new SaveService(directory);
            saves.Data.GamesPlayed = int.MaxValue;
            saves.Data.TotalScore = long.MaxValue - 1;
            saves.RecordGame(5);
            var data = new SaveService(directory).Data;
            Assert.That(data.GamesPlayed, Is.EqualTo(int.MaxValue));
            Assert.That(data.TotalScore, Is.EqualTo(long.MaxValue));
            Assert.That(data.HighScore, Is.EqualTo(5));
        }

        [Test]
        public void UnwritablePathDoesNotThrowOrDiscardInMemoryProgress()
        {
            string blockedPath = Path.Combine(directory, "file-instead-of-directory");
            File.WriteAllText(blockedPath, "occupied");
            var saves = new SaveService(blockedPath);
            LogAssert.Expect(LogType.Warning, new Regex("ROLOC could not save progress:"));
            Assert.DoesNotThrow(() => saves.RecordGame(8));
            Assert.That(saves.Data.HighScore, Is.EqualTo(8));
            Assert.That(File.ReadAllText(blockedPath), Is.EqualTo("occupied"));
        }
    }
}
