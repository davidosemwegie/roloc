using System;
using System.Linq;
using Roloc.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        RectTransform resultBadgeRow, resultBadgeViewport, resultBadgeContent;
        Text resultBadgeHeading;
        string resultBadgeKey;

        void BuildBadgeUI()
        {
            BuildPerfectFeedback();
            var menuButton = Button(menu, "BADGES", new Vector2(0, -28), new Vector2(100, 44),
                new Vector2(.5f, 1), Color.clear, Ink, () => ShowBadges(BadgeTrack.Run));
            menuButton.GetComponentInChildren<Text>().fontSize = 12;
            resultBadgeRow = Container(results, "New badges");
            resultBadgeHeading = Label(resultBadgeRow, "NEW BADGES", 10, Ink, new Vector2(0, 32), new Vector2(350, 18));
            resultBadgeContent = BadgeScroll(resultBadgeRow, new Vector2(0, -10), new Vector2(350, 62), true, out resultBadgeViewport);
            resultBadgeRow.gameObject.SetActive(false);
        }

        RectTransform BadgeScroll(Transform parent, Vector2 position, Vector2 size, bool horizontal, out RectTransform viewport)
        {
            viewport = Container(parent, "Badge viewport");
            Place(viewport, new Vector2(.5f, .5f), position, size);
            var input = viewport.gameObject.AddComponent<Image>(); input.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Container(viewport, "Badge content");
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = horizontal; scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        void RefreshResultBadges()
        {
            var ids = Saves.Data.ActiveRun?.NewBadges;
            bool any = ids != null && ids.Count > 0;
            resultBadgeRow.gameObject.SetActive(any);
            if (!any) return;
            string key = string.Join(",", ids);
            if (key == resultBadgeKey) return;
            resultBadgeKey = key;
            foreach (Transform child in resultBadgeContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            resultBadgeHeading.text = ids.Count > 4 ? "NEW BADGES · SWIPE TO SEE ALL" : ids.Count == 1 ? "NEW BADGE UNLOCKED" : "NEW BADGES UNLOCKED";
            for (int i = 0; i < ids.Count; i++)
            {
                var badge = BadgeCatalog.Find(ids[i]);
                if (badge != null) MakeBadge(resultBadgeContent, badge, new Vector2(38 + i * 76, -31), 59, false, false);
            }
            resultBadgeContent.sizeDelta = new Vector2(ids.Count * 76, 62);
            resultBadgeContent.anchoredPosition = Vector2.zero;
        }

        void MakeBadge(Transform parent, BadgeDefinition badge, Vector2 position, float size, bool locked, bool name)
        {
            int index = Array.IndexOf(BadgeCatalog.All, badge);
            int tier = badge.Track == BadgeTrack.Lifetime ? index - 12 : index;
            var button = Button(parent, "", position, new Vector2(size, size), new Vector2(0, 1),
                Color.clear, Ink, () => ShowBadgeDetail(badge));
            var art = Container(button.transform, badge.Name);
            Place(art, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * size * .92f);
            var pin = art.gameObject.AddComponent<BadgePinGraphic>();
            pin.Tier = tier; pin.Lifetime = badge.Track == BadgeTrack.Lifetime; pin.Locked = locked; pin.raycastTarget = false;
            var number = Label(art, badge.ShortValue, Mathf.RoundToInt(size * .23f), locked ? Muted : Ink,
                new Vector2(0, -size * .025f), new Vector2(size * .44f, size * .32f));
            number.fontStyle = FontStyle.Bold;
            number.resizeTextForBestFit = true; number.resizeTextMinSize = 8; number.resizeTextMaxSize = Mathf.RoundToInt(size * .23f);
            if (name)
            {
                Label(button.transform, badge.Name, 10, locked ? Muted : Ink,
                    new Vector2(0, -size * .59f), new Vector2(94, 29));
                Label(button.transform, locked ? "LOCKED" : "UNLOCKED", 8, Muted,
                    new Vector2(0, -size * .84f), new Vector2(94, 14));
            }
            else if (size < 100) Label(button.transform, badge.Track == BadgeTrack.Run ? "RUN" : "TOTAL", 7, Ink,
                new Vector2(0, -size * .40f), new Vector2(size * .7f, 10));
        }

        void ShowBadges(BadgeTrack track)
        {
            float height = Mathf.Clamp(safe.rect.height - 28, 360, 690);
            var list = BadgeCatalog.All.Where(badge => badge.Track == track).ToArray();
            int earned = list.Count(badge => Saves.Data.UnlockedBadges.Contains(badge.Id));
            var panel = NewOverlay("Your badges", earned + " / " + list.Length + " unlocked · " +
                (track == BadgeTrack.Run ? "Your best runs" : Saves.Data.TotalScore.ToString("N0") + " lifetime matches"), height);
            foreach (var choice in new[] { BadgeTrack.Run, BadgeTrack.Lifetime })
            {
                var selected = choice;
                var tab = Button(panel, choice == BadgeTrack.Run ? "RUN SCORES" : "LIFETIME", new Vector2(choice == BadgeTrack.Run ? -76 : 76, height / 2 - 150),
                    new Vector2(146, 38), new Vector2(.5f, .5f), track == choice ? Palette[1] : Color.clear,
                    track == choice ? Color.white : Ink, () => ShowBadges(selected));
                tab.GetComponentInChildren<Text>().fontSize = 12;
            }
            float viewHeight = height - 245;
            var content = BadgeScroll(panel, new Vector2(0, -62.5f), new Vector2(310, viewHeight), false, out _);
            content.sizeDelta = new Vector2(310, Mathf.Ceil(list.Length / 3f) * 118 + 10);
            for (int i = 0; i < list.Length; i++)
                MakeBadge(content, list[i], new Vector2(51 + i % 3 * 104, -48 - i / 3 * 118), 74,
                    !Saves.Data.UnlockedBadges.Contains(list[i].Id), true);
            Button(panel, "Done", new Vector2(0, -height / 2 + 35), new Vector2(270, 44), new Vector2(.5f, .5f),
                Palette[0], Color.white, () => overlay.gameObject.SetActive(false));
        }

        void ShowBadgeDetail(BadgeDefinition badge)
        {
            bool unlocked = Saves.Data.UnlockedBadges.Contains(badge.Id);
            string requirement = badge.Track == BadgeTrack.Run ? "Reach " + badge.Matches.ToString("N0") + " matches in one run."
                : "Make " + badge.Matches.ToString("N0") + " matches across all runs.";
            var panel = NewOverlay(badge.Name, requirement, 390);
            MakeBadge(panel, badge, new Vector2(169, -210), 130, !unlocked, false);
            Label(panel, unlocked ? "UNLOCKED · YOURS TO KEEP" : "KEEP PLAYING TO UNLOCK", 11, Ink, new Vector2(0, -90), new Vector2(300, 22));
            Button(panel, "All badges", new Vector2(0, -145), new Vector2(270, 44), new Vector2(.5f, .5f),
                Palette[0], Color.white, () => ShowBadges(badge.Track));
        }
    }
}
