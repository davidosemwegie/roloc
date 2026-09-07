using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        Text resultBrand;
        RectTransform resultMedalRow, resultMedal, resultFooter, resultReplay, resultMenu;
        Vector2 resultLayoutSize;

        void BuildResults()
        {
            resultBrand = Label(results, "RING RUSH", 12, Ink, Vector2.zero, Vector2.zero);
            resultTitle = Label(results, "Nice rush!", 39, Ink, Vector2.zero, Vector2.zero);
            resultTitle.fontStyle = FontStyle.Bold;
            resultReason = Label(results, "", 13, Muted, Vector2.zero, Vector2.zero);
            resultMedalRow = Container(results, "Score medal row");
            var medal = Circle(resultMedalRow, "Result ring", Palette[1], Vector2.zero, 226, true);
            resultMedal = medal.rectTransform;
            resultScore = Label(medal.transform, "0", 69, Ink, new Vector2(0, 9), new Vector2(190, 90));
            resultScore.fontStyle = FontStyle.Bold;
            resultScore.resizeTextForBestFit = true; resultScore.resizeTextMinSize = 30; resultScore.resizeTextMaxSize = 69;
            Label(medal.transform, "MATCHES", 10, Muted, new Vector2(0, -43), new Vector2(130, 22));
            resultBest = Label(results, "", 14, Muted, Vector2.zero, Vector2.zero);
            resultChains = Label(results, "", 12, Ink, Vector2.zero, Vector2.zero);
            resultProgress = Label(results, "", 12, Muted, Vector2.zero, Vector2.zero);
            dailyStandingLabel = Label(results, "", 11, Ink, Vector2.zero, Vector2.zero);
            resultReplay = (RectTransform)Button(results, "PLAY AGAIN", Vector2.zero, new Vector2(286, 60),
                new Vector2(.5f, .5f), Palette[0], Color.white, BeginRun).transform;
            resultFooter = Container(results, "Result actions");
            resultMenu = (RectTransform)Button(resultFooter, "Back to menu", Vector2.zero, new Vector2(170, 44),
                new Vector2(.5f, .5f), Color.clear, Muted, ShowMenu).transform;
            shareButton = Button(resultFooter, "Share Daily", Vector2.zero, new Vector2(170, 44),
                new Vector2(.5f, .5f), Color.clear, Palette[1], () => StartCoroutine(ShareDailyCard()));
            dailyStandingLabel.gameObject.SetActive(false); shareButton.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (results.gameObject.activeInHierarchy && resultLayoutSize != results.rect.size) LayoutResults();
        }

        void LayoutResults()
        {
            if (!resultFooter || !results.gameObject.activeInHierarchy) return;
            resultLayoutSize = results.rect.size;
            float width = Mathf.Min(365, resultLayoutSize.x - 32);
            float gap = Mathf.Clamp(resultLayoutSize.y * .014f, 8, 12);
            var labels = new[] { resultBrand, resultTitle, resultReason, resultBest, resultChains, resultProgress, dailyStandingLabel };
            float textHeight = 0;
            int textRows = 0;
            foreach (var label in labels)
            {
                if (!label.gameObject.activeSelf) continue;
                label.rectTransform.sizeDelta = new Vector2(width, 0);
                // Measure wrapped content before placing its neighbours. Text can no longer
                // overflow into a separately anchored record, progress message, or button.
                float height = Mathf.Ceil(label.preferredHeight) + 4;
                label.rectTransform.sizeDelta = new Vector2(width, height);
                textHeight += height; textRows++;
            }
            float fixedHeight = textHeight + 60 + 44 + (textRows + 2) * gap;
            float medalHeight = Mathf.Clamp(resultLayoutSize.y - 32 - fixedHeight, 48, 242);
            resultMedal.localScale = Vector3.one * ((medalHeight - 16) / 226);
            float y = Mathf.Max(16, (resultLayoutSize.y - fixedHeight - medalHeight) * .5f);
            PlaceResultRow(resultBrand.rectTransform, width, resultBrand.rectTransform.rect.height, ref y, gap);
            PlaceResultRow(resultTitle.rectTransform, width, resultTitle.rectTransform.rect.height, ref y, gap);
            PlaceResultRow(resultReason.rectTransform, width, resultReason.rectTransform.rect.height, ref y, gap);
            PlaceResultRow(resultMedalRow, width, medalHeight, ref y, gap);
            for (int i = 3; i < labels.Length; i++)
                if (labels[i].gameObject.activeSelf)
                    PlaceResultRow(labels[i].rectTransform, width, labels[i].rectTransform.rect.height, ref y, gap);
            PlaceResultRow(resultReplay, Mathf.Min(286, width), 60, ref y, gap);
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
