using System;
using Roloc.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        GameTelemetryClient telemetry;
        string telemetryRunId, telemetryMode;
        int telemetryRevives;
        double telemetryRunStartedAt;
        bool telemetryRunActive, telemetryFlushing, telemetryAway;
        long telemetryAwayAt;

        void InitializeTelemetry()
        {
            telemetry = new GameTelemetryClient(daily, SaveDirectoryOverride);
            CaptureTelemetry("game_opened");
            StartCoroutine(telemetry.Initialize(FlushTelemetry));
        }

        void CaptureTelemetry(string name, string mode = "") => telemetry?.Capture(name, mode);
        void CaptureRunTelemetry(string name)
        {
            if (telemetryRunActive) telemetry?.Capture(name, telemetryMode, telemetryRunId);
        }

        void StartTelemetryRun()
        {
            telemetryRunId = localRunId;
            telemetryMode = Session.Mode.ToString().ToLowerInvariant();
            telemetryRevives = 0;
            telemetryRunStartedAt = Time.realtimeSinceStartupAsDouble;
            telemetryRunActive = true;
            telemetry?.StartRun(telemetryRunId, telemetryMode,
                dailyRun ? "" : regularTicket?.runId ?? "", dailyRun ? dailyAttempt?.attemptId ?? "" : "");
            FlushTelemetry();
        }

        double TelemetryElapsed() => Math.Max(0, (Time.realtimeSinceStartupAsDouble - telemetryRunStartedAt) * 1000);

        void EndTelemetryRun(bool abandoned)
        {
            if (!telemetryRunActive) return;
            telemetryRunActive = false;
            if (abandoned) telemetry?.AbandonRun(telemetryRunId, Session.Score, telemetryRevives, TelemetryElapsed());
            else telemetry?.CompleteRun(telemetryRunId, Session.Score, telemetryRevives, TelemetryElapsed());
            FlushTelemetry();
        }

        void FlushTelemetry()
        {
            if (telemetry == null || telemetryFlushing) return;
            telemetryFlushing = true;
            StartCoroutine(telemetry.Flush(() => telemetryFlushing = false, _ => telemetryFlushing = false));
        }

        void TelemetryForegroundChanged()
        {
            bool away = applicationPaused || !applicationFocused;
            if (away == telemetryAway) return;
            telemetryAway = away;
            if (away)
            {
                telemetryAwayAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                SnapshotTelemetryRun();
                FlushTelemetry();
            }
            else
            {
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - telemetryAwayAt >= 30 * 60 * 1000)
                    CaptureTelemetry("game_opened");
                FlushTelemetry();
            }
        }

        void SnapshotTelemetryRun()
        {
            if (telemetryRunActive) telemetry?.UpdateRun(telemetryRunId, Session.Score, telemetryRevives, TelemetryElapsed());
        }

        void OnApplicationQuit() { SnapshotTelemetryRun(); FlushTelemetry(); }

        void ShowAnalyticsSettings(Action back)
        {
            var panel = NewOverlay("Usage analytics", "Share gameplay statistics to help improve Ring Rush.", 420);
            if (!telemetry.AnalyticsAvailable)
            {
                Label(panel, "Usage analytics is unavailable in this build.\nYour game history continues to be saved.\nYour analytics preference is unchanged.", 14, Muted,
                    new Vector2(0, -4), new Vector2(296, 85));
                var unavailable = Button(panel, "ANALYTICS UNAVAILABLE", new Vector2(0, -83), new Vector2(270, 48),
                    new Vector2(.5f, .5f), Muted, Color.white, () => { });
                unavailable.interactable = false;
                Button(panel, "Back", new Vector2(0, -154), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Ink, back);
                return;
            }
            int generation = leaderboardViewGeneration;
            var explanation = Label(panel, "Turning this off stops usage analytics.\nYour game history and leaderboard records\ncontinue to be saved.", 14, Muted,
                new Vector2(0, -4), new Vector2(296, 85));
            Button toggle = null;
            toggle = Button(panel, telemetry.AnalyticsEnabled ? "ANALYTICS ON" : "ANALYTICS OFF", new Vector2(0, -83), new Vector2(270, 48),
                new Vector2(.5f, .5f), Palette[1], Color.white, () => {
                    bool enabled = !telemetry.AnalyticsEnabled;
                    toggle.interactable = false;
                    toggle.GetComponentInChildren<Text>().text = enabled ? "ANALYTICS ON" : "ANALYTICS OFF";
                    StartCoroutine(telemetry.SetAnalyticsEnabled(enabled, () => {
                        if (!LeaderboardViewCurrent(generation, panel)) return;
                        toggle.GetComponentInChildren<Text>().text = telemetry.AnalyticsEnabled ? "ANALYTICS ON" : "ANALYTICS OFF";
                        toggle.interactable = true;
                    }, error => {
                        if (!LeaderboardViewCurrent(generation, panel)) return;
                        toggle.GetComponentInChildren<Text>().text = telemetry.AnalyticsEnabled ? "ANALYTICS ON" : "ANALYTICS OFF";
                        toggle.interactable = true;
                        explanation.text = "Preference saved on this device.\nIt will sync when you reconnect.\nGame history remains enabled.";
                    }));
                });
            Button(panel, "Back", new Vector2(0, -154), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Ink, back);
        }
    }
}
