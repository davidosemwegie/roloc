using Roloc.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        RectTransform resultHeader, resultLogo, resultModePill, resultStats, resultReward;
        RectTransform resultMedalRow, resultMedal, resultFooter, resultReplay, resultMenu;
        readonly RectTransform[] resultStatCards = new RectTransform[3];
        readonly Text[] resultStatValues = new Text[3], resultStatBadges = new Text[3];
        readonly SoftShape[] resultDecorations = new SoftShape[3];
        SoftShape resultRewardArt, resultRewardPreview, resultRewardTrack, resultRewardFill;
        Text resultRewardTitle;
        Vector2 resultLayoutSize;
        float resultRewardFraction;

        void BuildResults()
        {
            resultHeader = Container(results, "Result header");
            resultLogo = Container(resultHeader, "Result wordmark");
            Logo(resultLogo, Vector2.zero, new Vector2(67, 45), new Vector2(.5f, .5f));
            var pill = Box(resultHeader, "Mode badge", new Color32(222, 232, 255, 255), Vector2.zero, new Vector2(146, 28));
            pill.shadow = pill.shaded = false; pill.cornerRadius = 14; resultModePill = pill.rectTransform;
            resultBest = Label(pill.transform, "", 11, Ink, Vector2.zero, new Vector2(140, 24));
            resultTitle = Label(results, "Nice rush!", 34, Ink, Vector2.zero, Vector2.zero);
            resultTitle.fontStyle = FontStyle.Bold;
            resultReason = Label(results, "", 13, Muted, Vector2.zero, Vector2.zero);

            resultMedalRow = Container(results, "Score medal row");
            resultMedal = Container(resultMedalRow, "Score sculpture");
            Place(resultMedal, new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 220));
            var center = Circle(resultMedal, "Score face", Color.white, Vector2.zero, 190, false);
            center.shadow = center.shaded = false;
            var medal = Circle(resultMedal, "Result ring", Palette[1], Vector2.zero, 206, true);
            medal.thickness = .18f;
            resultScore = Label(resultMedal, "0", 65, Ink, new Vector2(0, 8), new Vector2(155, 82));
            resultScore.fontStyle = FontStyle.Bold;
            resultScore.resizeTextForBestFit = true; resultScore.resizeTextMinSize = 28; resultScore.resizeTextMaxSize = 65;
            Label(resultMedal, "MATCHES", 10, Muted, new Vector2(0, -39), new Vector2(130, 22));
            resultDecorations[0] = Circle(resultMedal, "Orange accent ring", Palette[0], new Vector2(117, 63), 32, true);
            resultDecorations[1] = Circle(resultMedal, "Pink accent puck", Palette[3], new Vector2(-115, -60), 25, false);
            resultDecorations[2] = Circle(resultMedal, "Lime accent puck", Palette[2], new Vector2(-121, 68), 17, false);

            foreach (var decoration in resultDecorations) { decoration.shadow = false; decoration.depth = 2; }

            resultStats = Container(results, "Run records");
            string[] labels = { "PERSONAL BEST", "BEST COMBO", "PERFECT STREAK" };
            Color[] fills = { new Color32(222, 232, 255, 255), new Color32(255, 221, 234, 255), new Color32(233, 245, 191, 255) };
            Color[] inks = { new Color32(37, 65, 163, 255), new Color32(145, 38, 85, 255), new Color32(69, 92, 22, 255) };
            for (int i = 0; i < 3; i++)
            {
                var card = Box(resultStats, labels[i], fills[i], Vector2.zero, Vector2.zero);
                card.shadow = card.shaded = false; card.cornerRadius = 14; resultStatCards[i] = card.rectTransform;
                Label(card.transform, labels[i], 12, inks[i], new Vector2(0, 23), new Vector2(106, 16));
                resultStatValues[i] = Label(card.transform, "0", 30, inks[i], new Vector2(0, 0), new Vector2(100, 38));
                resultStatValues[i].resizeTextForBestFit = true; resultStatValues[i].resizeTextMinSize = 16; resultStatValues[i].resizeTextMaxSize = 30;
                resultStatBadges[i] = Label(card.transform, "", 10, inks[i], new Vector2(0, -24), new Vector2(100, 14));
            }
            resultRewardArt = Box(results, "Progress reward", Color.white, Vector2.zero, Vector2.zero);
            resultRewardArt.shadow = resultRewardArt.shaded = false; resultRewardArt.cornerRadius = 16;
            resultReward = resultRewardArt.rectTransform;
            resultRewardTitle = Label(resultReward, "", 15, Ink, Vector2.zero, Vector2.zero);
            resultRewardTitle.alignment = TextAnchor.MiddleLeft;
            resultProgress = Label(resultReward, "", 12, Muted, Vector2.zero, Vector2.zero);
            resultProgress.alignment = TextAnchor.MiddleLeft;
            resultRewardPreview = Circle(resultReward, "Next reward preview", Palette[1], Vector2.zero, 43, true);
            resultRewardPreview.shadow = false; resultRewardPreview.depth = 2;
            resultRewardTrack = Box(resultReward, "Progress track", new Color32(224, 233, 247, 255), Vector2.zero, Vector2.zero);
            resultRewardTrack.shadow = resultRewardTrack.shaded = false; resultRewardTrack.cornerRadius = 3;
            resultRewardFill = Box(resultRewardTrack.transform, "Earned progress", Palette[1], Vector2.zero, Vector2.zero);
            resultRewardFill.shadow = resultRewardFill.shaded = false; resultRewardFill.cornerRadius = 3;
            dailyStandingLabel = Label(results, "", 11, Ink, Vector2.zero, Vector2.zero);
            resultReplay = (RectTransform)Button(results, "PLAY AGAIN", Vector2.zero, new Vector2(320, 56),
                new Vector2(.5f, .5f), Palette[0], Color.white, BeginRun).transform;
            resultFooter = Container(results, "Result actions");
            resultMenu = (RectTransform)Button(resultFooter, "Back to menu", Vector2.zero, new Vector2(170, 44),
                new Vector2(.5f, .5f), Color.clear, Muted, ShowMenu).transform;
            shareButton = Button(resultFooter, "Share Daily", Vector2.zero, new Vector2(170, 44),
                new Vector2(.5f, .5f), Color.clear, Palette[1], () => StartCoroutine(ShareDailyCard()));
            dailyStandingLabel.gameObject.SetActive(false); shareButton.gameObject.SetActive(false);
        }

        void RefreshResultRewards()
        {
            resultStatValues[0].text = CurrentRecord().HighScore.ToString();
            resultStatValues[1].text = Session.BestCombo.ToString();
            resultStatValues[2].text = Session.BestPerfectStreak.ToString();
            resultStatBadges[0].text = Session.Score > startingBest ? "NEW BEST!" : "";
            resultStatBadges[1].text = Session.BestCombo > startingCombo ? "NEW BEST!" : "";
            resultStatBadges[2].text = Session.BestPerfectStreak > startingPerfect ? "NEW BEST!" : "";
            foreach (var decoration in resultDecorations) decoration.gameObject.SetActive(!Saves.Data.ReduceEffects);
            long earned = Saves.Data.ProgressPoints - startingPoints;
            CosmeticDefinition unlocked = null;
            foreach (var item in CosmeticCatalog.Unlocks)
                if (startingPoints < item.UnlockAt && Saves.Data.ProgressPoints >= item.UnlockAt) unlocked = item;
            var snapshot = Saves.GetProgressSnapshot();
            resultRewardTitle.text = unlocked != null ? "Unlocked " + unlocked.Name + "!" : "+" + earned + " progress";
            string next = snapshot.AllUnlocked
                ? snapshot.TotalMatches + " lifetime matches · " + snapshot.MatchesToMilestone + " to " + snapshot.NextMatchMilestone
                : snapshot.PointsToNextUnlock + " more progress to " + snapshot.NextUnlock.Name;
            resultProgress.text = unlocked != null ? "+" + earned + " progress\n" + next : next;
            resultRewardArt.color = unlocked != null ? new Color32(239, 248, 212, 255) : Color.white;
            var preview = unlocked ?? snapshot.NextUnlock;
            resultRewardPreview.Finish = preview?.Id ?? "orbit";
            resultRewardPreview.kind = preview == null || preview.Category == CosmeticCategory.Ring ? SoftShape.Shape.Ring :
                preview.Category == CosmeticCategory.Trail ? SoftShape.Shape.Arc : SoftShape.Shape.Disc;
            resultRewardPreview.progress = .75f;
            resultRewardPreview.color = preview?.Category == CosmeticCategory.Background ? new Color32(158, 151, 202, 255) : Palette[1];
            long previous = 0;
            if (!snapshot.AllUnlocked)
                foreach (var item in CosmeticCatalog.Unlocks) if (item.UnlockAt <= snapshot.Points) previous = item.UnlockAt;
            resultRewardFraction = snapshot.AllUnlocked ? (float)snapshot.TotalMatches / snapshot.NextMatchMilestone
                : (float)(snapshot.Points - previous) / (snapshot.NextUnlock.UnlockAt - previous);
            resultRewardFraction = Mathf.Clamp01(resultRewardFraction);
        }

        void LateUpdate()
        {
            if (results.gameObject.activeInHierarchy && resultLayoutSize != results.rect.size) LayoutResults();
        }

        float MeasureResultText(Text label, float width)
        {
            label.rectTransform.sizeDelta = new Vector2(width, 0);
            float height = Mathf.Ceil(label.preferredHeight) + 4;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            return height;
        }

        void LayoutResults()
        {
            if (!resultFooter || !results.gameObject.activeInHierarchy) return;
            resultLayoutSize = results.rect.size;
            float width = Mathf.Min(350, resultLayoutSize.x - 32);
            bool compact = resultLayoutSize.y < 720;
            float gap = compact ? 6 : 10, headerHeight = compact ? 34 : 44;
            resultTitle.fontSize = Session.Score > startingBest ? 28 : compact ? 30 : 34;
            float titleHeight = MeasureResultText(resultTitle, width);
            float reasonHeight = MeasureResultText(resultReason, width);
            float rewardTextWidth = width - 86;
            float rewardTitleHeight = MeasureResultText(resultRewardTitle, rewardTextWidth);
            float rewardBodyHeight = MeasureResultText(resultProgress, rewardTextWidth);
            float rewardHeight = Mathf.Max(82, rewardTitleHeight + rewardBodyHeight + 36);
            float rankingHeight = dailyStandingLabel.gameObject.activeSelf ? MeasureResultText(dailyStandingLabel, width) : 0;
            int rows = rankingHeight > 0 ? 9 : 8;
            float fixedHeight = headerHeight + titleHeight + reasonHeight + 78 + rewardHeight + rankingHeight + 56 + 44 + (rows - 1) * gap;
            float medalHeight = Mathf.Clamp(resultLayoutSize.y - 24 - fixedHeight, 48, 226);
            resultMedal.localScale = Vector3.one * Mathf.Min(width / 300, medalHeight / 220);
            float y = Mathf.Max(12, (resultLayoutSize.y - fixedHeight - medalHeight) * .5f);
            PlaceResultRow(resultHeader, width, headerHeight, ref y, gap);
            resultLogo.anchoredPosition = new Vector2(-width * .5f + 35, 0);
            resultLogo.localScale = Vector3.one * (compact ? .8f : 1);
            resultModePill.anchoredPosition = new Vector2(width * .5f - 73, 0);
            PlaceResultRow(resultTitle.rectTransform, width, titleHeight, ref y, gap);
            PlaceResultRow(resultReason.rectTransform, width, reasonHeight, ref y, gap);
            PlaceResultRow(resultMedalRow, width, medalHeight, ref y, gap);
            PlaceResultRow(resultStats, width, 78, ref y, gap);
            for (int i = 0; i < 3; i++)
            {
                float cardWidth = (width - 16) / 3;
                Place(resultStatCards[i], new Vector2(.5f, .5f), new Vector2((i - 1) * (cardWidth + 8), 0), new Vector2(cardWidth, 78));
            }
            PlaceResultRow(resultReward, width, rewardHeight, ref y, gap);
            Place(resultRewardTitle.rectTransform, new Vector2(0, 1), new Vector2(16 + rewardTextWidth * .5f, -12 - rewardTitleHeight * .5f), new Vector2(rewardTextWidth, rewardTitleHeight));
            Place(resultProgress.rectTransform, new Vector2(0, 1), new Vector2(16 + rewardTextWidth * .5f, -12 - rewardTitleHeight - rewardBodyHeight * .5f), new Vector2(rewardTextWidth, rewardBodyHeight));
            Place(resultRewardPreview.rectTransform, new Vector2(1, 1), new Vector2(-35, -35), new Vector2(43, 43));
            Place(resultRewardTrack.rectTransform, new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(width - 32, 6));
            resultRewardFill.rectTransform.pivot = new Vector2(0, .5f);
            resultRewardFill.rectTransform.anchorMin = resultRewardFill.rectTransform.anchorMax = new Vector2(0, .5f);
            resultRewardFill.rectTransform.anchoredPosition = Vector2.zero;
            resultRewardFill.rectTransform.sizeDelta = new Vector2((width - 32) * resultRewardFraction, 6);
            resultRewardFill.gameObject.SetActive(resultRewardFraction > 0);
            if (rankingHeight > 0) PlaceResultRow(dailyStandingLabel.rectTransform, width, rankingHeight, ref y, gap);
            PlaceResultRow(resultReplay, width, 56, ref y, gap);
            PlaceResultRow(resultFooter, width, 44, ref y, gap);
            resultMenu.anchoredPosition = new Vector2(dailyRun ? -width * .25f : 0, 0);
            ((RectTransform)shareButton.transform).anchoredPosition = new Vector2(width * .25f, 0);
        }

        static void PlaceResultRow(RectTransform row, float width, float height, ref float y, float gap)
        {
            Place(row, new Vector2(.5f, 1), new Vector2(0, -y - height * .5f), new Vector2(width, height));
            y += height + gap;
        }
    }
}
