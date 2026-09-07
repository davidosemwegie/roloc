using Roloc.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        RectTransform resultHeader, resultLogo, resultMode, resultStats, resultReward;
        RectTransform resultMedalRow, resultMedal, resultFooter, resultReplay, resultMenu;
        readonly RectTransform[] resultStatColumns = new RectTransform[3];
        readonly Text[] resultStatValues = new Text[3], resultStatBadges = new Text[3];
        SoftShape resultRewardPreview, resultRewardTrack, resultRewardFill;
        Text resultRewardTitle;
        Vector2 resultLayoutSize;
        float resultRewardFraction;

        void BuildResults()
        {
            resultHeader = Container(results, "Result header");
            resultLogo = Container(resultHeader, "Result wordmark");
            Logo(resultLogo, Vector2.zero, new Vector2(67, 45), new Vector2(.5f, .5f));
            resultMode = Container(resultHeader, "Mode");
            Place(resultMode, new Vector2(.5f, .5f), Vector2.zero, new Vector2(146, 28));
            resultBest = Label(resultMode, "", 11, Ink, Vector2.zero, new Vector2(146, 24));
            resultBest.alignment = TextAnchor.MiddleRight;
            resultTitle = Label(results, "Nice rush!", 34, Ink, Vector2.zero, Vector2.zero);
            resultTitle.fontStyle = FontStyle.Bold;
            resultReason = Label(results, "", 13, Muted, Vector2.zero, Vector2.zero);

            resultMedalRow = Container(results, "Score medal row");
            resultMedal = Container(resultMedalRow, "Score sculpture");
            Place(resultMedal, new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 260));
            var medal = Circle(resultMedal, "Result ring", Palette[1], Vector2.zero, 248, true);
            medal.thickness = .16f;
            resultScore = Label(resultMedal, "0", 82, Ink, new Vector2(0, 9), new Vector2(184, 102));
            resultScore.fontStyle = FontStyle.Bold;
            resultScore.resizeTextForBestFit = true; resultScore.resizeTextMinSize = 28; resultScore.resizeTextMaxSize = 82;
            Label(resultMedal, "MATCHES", 11, Ink, new Vector2(0, -47), new Vector2(130, 22));

            resultStats = Container(results, "Run records");
            string[] labels = { "BEST", "COMBO", "PERFECT STREAK" };
            Color[] inks = { Ink, Palette[1], new Color32(185, 32, 104, 255) };
            for (int i = 0; i < 3; i++)
            {
                var column = Container(resultStats, labels[i]);
                resultStatColumns[i] = column;
                resultStatValues[i] = Label(column, "0", 32, inks[i], new Vector2(0, 16), new Vector2(100, 40));
                resultStatValues[i].fontStyle = FontStyle.Bold;
                resultStatValues[i].resizeTextForBestFit = true; resultStatValues[i].resizeTextMinSize = 16; resultStatValues[i].resizeTextMaxSize = 32;
                Label(column, labels[i], 10, Ink, new Vector2(0, -13), new Vector2(106, 16));
                resultStatBadges[i] = Label(column, "", 10, inks[i], new Vector2(0, -31), new Vector2(100, 14));
            }
            resultReward = Container(results, "Progress reward");
            resultRewardTitle = Label(resultReward, "", 19, Palette[1], Vector2.zero, Vector2.zero);
            resultRewardTitle.fontStyle = FontStyle.Bold;
            resultRewardTitle.alignment = TextAnchor.MiddleLeft;
            resultProgress = Label(resultReward, "", 12, Muted, Vector2.zero, Vector2.zero);
            resultProgress.alignment = TextAnchor.MiddleLeft;
            resultRewardPreview = Circle(resultReward, "Next reward preview", Palette[1], Vector2.zero, 58, true);
            resultRewardPreview.shadow = false; resultRewardPreview.depth = 4;
            resultRewardTrack = Box(resultReward, "Progress track", new Color32(224, 233, 247, 255), Vector2.zero, Vector2.zero);
            resultRewardTrack.shadow = resultRewardTrack.shaded = false; resultRewardTrack.cornerRadius = 3;
            resultRewardFill = Box(resultRewardTrack.transform, "Earned progress", Palette[2], Vector2.zero, Vector2.zero);
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
            float gap = compact ? 7 : 16, headerHeight = compact ? 34 : 44;
            resultTitle.fontSize = Session.Score > startingBest ? 28 : compact ? 32 : 38;
            float titleHeight = MeasureResultText(resultTitle, width);
            float reasonHeight = MeasureResultText(resultReason, width);
            float rewardTextWidth = width - 80;
            float rewardTitleHeight = MeasureResultText(resultRewardTitle, rewardTextWidth);
            float rewardBodyHeight = MeasureResultText(resultProgress, rewardTextWidth);
            float rewardHeight = Mathf.Max(90, rewardTitleHeight + rewardBodyHeight + 28);
            float rankingHeight = dailyStandingLabel.gameObject.activeSelf ? MeasureResultText(dailyStandingLabel, width) : 0;
            int rows = rankingHeight > 0 ? 9 : 8;
            float fixedHeight = headerHeight + titleHeight + reasonHeight + 78 + rewardHeight + rankingHeight + 56 + 44 + (rows - 1) * gap;
            float medalHeight = Mathf.Clamp(resultLayoutSize.y - 24 - fixedHeight, 48, 270);
            resultMedal.localScale = Vector3.one * Mathf.Min(width / 300, medalHeight / 260);
            float y = Mathf.Max(12, (resultLayoutSize.y - fixedHeight - medalHeight) * .5f);
            PlaceResultRow(resultHeader, width, headerHeight, ref y, gap);
            resultLogo.anchoredPosition = new Vector2(-width * .5f + 35, 0);
            resultLogo.localScale = Vector3.one * (compact ? .8f : 1);
            resultMode.anchoredPosition = new Vector2(width * .5f - 73, 0);
            PlaceResultRow(resultTitle.rectTransform, width, titleHeight, ref y, gap);
            PlaceResultRow(resultReason.rectTransform, width, reasonHeight, ref y, gap);
            PlaceResultRow(resultMedalRow, width, medalHeight, ref y, gap);
            PlaceResultRow(resultStats, width, 78, ref y, gap);
            for (int i = 0; i < 3; i++)
            {
                float columnWidth = (width - 16) / 3;
                Place(resultStatColumns[i], new Vector2(.5f, .5f), new Vector2((i - 1) * (columnWidth + 8), 0), new Vector2(columnWidth, 78));
            }
            PlaceResultRow(resultReward, width, rewardHeight, ref y, gap);
            Place(resultRewardTitle.rectTransform, new Vector2(0, 1), new Vector2(80 + rewardTextWidth * .5f, -rewardTitleHeight * .5f), new Vector2(rewardTextWidth, rewardTitleHeight));
            Place(resultProgress.rectTransform, new Vector2(0, 1), new Vector2(80 + rewardTextWidth * .5f, -rewardTitleHeight - rewardBodyHeight * .5f), new Vector2(rewardTextWidth, rewardBodyHeight));
            Place(resultRewardPreview.rectTransform, new Vector2(0, 1), new Vector2(31, -32), new Vector2(62, 62));
            Place(resultRewardTrack.rectTransform, new Vector2(.5f, 0), new Vector2(40, 10), new Vector2(width - 80, 6));
            resultRewardFill.rectTransform.pivot = new Vector2(0, .5f);
            resultRewardFill.rectTransform.anchorMin = resultRewardFill.rectTransform.anchorMax = new Vector2(0, .5f);
            resultRewardFill.rectTransform.anchoredPosition = Vector2.zero;
            resultRewardFill.rectTransform.sizeDelta = new Vector2((width - 80) * resultRewardFraction, 6);
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
