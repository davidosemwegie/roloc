using Roloc.Core;
using UnityEngine;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        readonly SoftShape[] perfectRipples = new SoftShape[2];
        float perfectFeedbackLeft;
        Color perfectFeedbackTint;

        void BuildPerfectFeedback()
        {
            for (int i = 0; i < perfectRipples.Length; i++)
            {
                var ring = Circle(board, "Perfect ripple " + i, Color.white, Vector2.zero, 137, true);
                ring.shadow = ring.shaded = false; ring.thickness = .025f;
                ring.gameObject.SetActive(false); perfectRipples[i] = ring;
            }
        }
        void StartPerfectFeedback(Vector2 position, Color tint)
        {
            if (Saves.Data.ReduceEffects) return;
            perfectFeedbackLeft = .35f; perfectFeedbackTint = tint;
            for (int i = 0; i < perfectRipples.Length; i++)
            {
                perfectRipples[i].rectTransform.anchoredPosition = position;
                perfectRipples[i].rectTransform.localScale = Vector3.one;
                perfectRipples[i].color = Color.clear;
            }
        }
        void UpdatePerfectFeedback()
        {
            if (Session == null || !perfectRipples[0]) return;
            if (Session.State == RoundState.Menu || Session.State == RoundState.GameOver || Saves.Data.ReduceEffects)
                perfectFeedbackLeft = 0;
            if (Session.State != RoundState.Paused) perfectFeedbackLeft = Mathf.Max(0, perfectFeedbackLeft - Time.unscaledDeltaTime);
            for (int i = 0; i < perfectRipples.Length; i++)
            {
                float elapsed = .35f - perfectFeedbackLeft - i * .075f;
                bool visible = perfectFeedbackLeft > 0 && elapsed >= 0 && Session.State != RoundState.Paused;
                perfectRipples[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float fraction = Mathf.Clamp01(elapsed / (.35f - i * .075f));
                var tint = perfectFeedbackTint; tint.a = (1 - fraction) * .65f;
                perfectRipples[i].color = tint;
                perfectRipples[i].rectTransform.localScale = Vector3.one * (1 + fraction * .35f);
            }
        }
    }
}
