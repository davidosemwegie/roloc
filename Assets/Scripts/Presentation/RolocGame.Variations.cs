using Roloc.Core;
using UnityEngine;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        VariationPalettes variationPalettes;
        CanvasGroup boardVisibility;
        FlowMode displayedVariation;
        int displayedPalette = -1;
        int[] displayedRingOrder, displayedPuckOrder;
        float orbitSeconds, paletteMoveSeconds, layoutBlend, layoutFrom, layoutTo;
        bool paletteChanging, paletteApplied, colorHintShown;

        static bool Orbit(FlowMode mode) => VariationMotion.HasOrbit(mode);
        static bool Expanded(FlowMode mode) => Orbit(mode) || mode == FlowMode.FloatingDrifting;
        static Vector2 Radial(float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }
        Color BoardTint(int identity) => displayedPalette >= 0
            ? variationPalettes.Get(displayedPalette, identity) : Palette[identity];

        void InitializeVariationPresentation()
        {
            variationPalettes = Resources.Load<VariationPalettes>("VariationPalettes");
            if (!variationPalettes) variationPalettes = ScriptableObject.CreateInstance<VariationPalettes>();
            boardVisibility = board.gameObject.AddComponent<CanvasGroup>();
        }

        void ClearBoardEffects()
        {
            rippleTime = 0; ripple.gameObject.SetActive(false);
            perfectFeedbackLeft = 0;
            foreach (var effect in perfectRipples)
                if (effect) effect.gameObject.SetActive(false);
            for (int i = 0; i < trail.Length; i++)
            {
                trailLife[i] = 0;
                if (trail[i]) trail[i].gameObject.SetActive(false);
            }
        }

        void ApplyBoardPalette(int index)
        {
            displayedPalette = index;
            for (int c = 0; c < 4; c++)
            {
                var tint = BoardTint(c);
                ringArt[c].color = tint; pucks[c].SetTint(tint);
            }
            timerFill.color = BoardTint(Session.ActiveColor);
        }

        void RefreshPuckSymbols()
        {
            for (int c = 0; c < 4; c++)
            {
                if (!symbols[c]) continue;
                Color tint = BoardTint(c).linear;
                float luminance = .2126f * tint.r + .7152f * tint.g + .0722f * tint.b;
                symbols[c].color = c == Session.ActiveColor && luminance < .22f ? Color.white : Ink;
            }
        }

        void ResetVariations()
        {
            displayedVariation = FlowMode.Steady;
            displayedRingOrder = Session.RingOrder; displayedPuckOrder = Session.PuckOrder;
            orbitSeconds = layoutBlend = layoutFrom = layoutTo = 0;
            paletteChanging = colorHintShown = false;
            boardVisibility.alpha = 1;
            foreach (var puck in pucks) puck.FollowHomeExactly = false;
            ApplyBoardPalette(-1); ClearBoardEffects();
        }

        Vector2 RegularRingHome(int identity)
        {
            int slot = System.Array.IndexOf(Session.RingOrder, identity);
            if (slot < 0) return rings[identity].anchoredPosition;
            if (Expanded(Session.FlowMode))
            {
                float angle = SlotAngle(slot);
                if (VariationMotion.OrbitsRings(Session.FlowMode))
                    angle -= Session.RingOrbitDirection * difficulty.Variations.RingOrbitDegreesPerSecond * orbitSeconds;
                return Radial(220, angle) + DriftOffset(identity);
            }
            return RingSlots[slot] + DriftOffset(identity);
        }

        Vector2 RegularPuckHome(int identity)
        {
            int slot = System.Array.IndexOf(Session.PuckOrder, identity);
            if (slot < 0) return pucks[identity].Home;
            if (Expanded(Session.FlowMode))
            {
                float angle = SlotAngle(slot);
                if (VariationMotion.OrbitsPucks(Session.FlowMode))
                    angle -= Session.PuckOrbitDirection * difficulty.Variations.PuckOrbitDegreesPerSecond * orbitSeconds;
                return Radial(82, angle);
            }
            return PuckSlots[slot];
        }
        static float SlotAngle(int slot) => slot == 0 ? 135 : slot == 1 ? 45 : slot == 2 ? 225 : 315;

        void PrepareVariationTransition()
        {
            if (displayedVariation != Session.FlowMode) orbitSeconds = 0;
            layoutFrom = layoutBlend; layoutTo = Expanded(Session.FlowMode) ? 1 : 0;
            paletteChanging = displayedPalette != Session.PaletteIndex;
            paletteApplied = false;
            if (paletteChanging || displayedVariation != Session.FlowMode)
                ClearBoardEffects();
            if (Session.FlowMode == FlowMode.ColorShift && !colorHintShown)
            { Hint("Colors changed. Match the exact shade.", 3); colorHintShown = true; }

        }

        void SetVariationTransitionDuration()
        {
            bool layoutChanged = displayedVariation != Session.FlowMode &&
                (Expanded(displayedVariation) || Expanded(Session.FlowMode));
            if (layoutChanged) transitionDuration = Mathf.Max(transitionDuration, .75f);
            if (paletteChanging)
            {
                paletteMoveSeconds = displayedRingOrder != Session.RingOrder || displayedPuckOrder != Session.PuckOrder ? transitionDuration : 0;
                transitionDuration = paletteMoveSeconds + .45f;
            }
        }

        float AnimateVariationTransition(float progress)
        {
            float move = progress;
            if (paletteChanging)
            {
                float elapsed = progress * transitionDuration;
                move = paletteMoveSeconds > 0 ? Mathf.Clamp01(elapsed / paletteMoveSeconds) : 1;
                float fade = Mathf.Clamp01((elapsed - paletteMoveSeconds) / .45f);
                bool swappedNow = fade >= .5f && !paletteApplied;
                if (swappedNow)
                {
                    ApplyBoardPalette(Session.PaletteIndex); paletteApplied = true;
                }
                boardVisibility.alpha = Saves.Data.ReduceEffects ? 1 : swappedNow ? 0 : Mathf.Abs(2 * fade - 1);
            }
            layoutBlend = Mathf.Lerp(layoutFrom, layoutTo, Mathf.SmoothStep(0, 1, move));
            return move;
        }

        void FinishVariationTransition()
        {
            ApplyBoardPalette(Session.PaletteIndex);
            boardVisibility.alpha = 1; paletteChanging = false;
            displayedVariation = Session.FlowMode;
            displayedRingOrder = Session.RingOrder; displayedPuckOrder = Session.PuckOrder;
            layoutBlend = layoutTo;
        }

        void FitVariationBoard()
        {
            if (Session.IsDaily || Session.WasTutorial)
            {
                board.anchorMin = board.anchorMax = new Vector2(.5f, .43f);
                board.anchoredPosition = Vector2.zero;
                float originalScale = Mathf.Min(1, Mathf.Min((safe.rect.width - 16) / 350, (safe.rect.height - 275) / 440));
                board.localScale = Vector3.one * Mathf.Max(.4f, originalScale);
                return;
            }
            // The playable rectangle excludes the HUD and bottom instructions.
            float available = Mathf.Max(60, safe.rect.height - 353);
            float width = Mathf.Lerp(350, 600, layoutBlend), height = Mathf.Lerp(440, 600, layoutBlend);
            float scale = Mathf.Min(1, Mathf.Min((safe.rect.width - 20) / width, available / height));
            board.anchorMin = board.anchorMax = new Vector2(.5f, 0);
            board.anchoredPosition = new Vector2(0, 118 + available * .5f);
            board.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }

        string VariationCaption()
        {
            switch (Session.FlowMode)
            {
                case FlowMode.FloatingDrifting: return "FLOAT AND DRIFT.";
                case FlowMode.PuckOrbitDrifting: return "ORBIT AND DRIFT.";
                case FlowMode.RingOrbitFloating: return "FLOAT INTO ORBIT.";
                case FlowMode.ColorShift: return "MATCH THE SHADE.";
                case FlowMode.PuckOrbit: return "PUCKS ON THE MOVE.";
                case FlowMode.RingOrbit: return "FOLLOW THE RINGS.";
                case FlowMode.DualOrbit: return "EVERYTHING IN ORBIT.";
                case FlowMode.Floating: return "LET IT FLOAT.";
                case FlowMode.Drifting: return "FOLLOW THE DRIFT.";
                case FlowMode.Breather: return "TAKE A BREATH.";
                default: return "A FRESH PERSPECTIVE.";
            }
        }
    }
}
