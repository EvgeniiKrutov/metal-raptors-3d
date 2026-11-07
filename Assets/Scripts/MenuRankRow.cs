using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuRankRow
    {
        const float StartDelaySec = 0.35f;
        const float FullFillSec = 1.1f;
        const float MinFillSec = 0.25f;
        const float PromoteHoldSec = 0.2f;
        const float PopSec = 0.3f;
        const float PopScale = 1.35f;
        const float MaxStep = 0.05f;

        public static float Height => MenuTheme.RankBadgeSize;

        readonly RectTransform _fill;
        readonly Image _badge;
        readonly Text _name;

        MenuRankRow(RectTransform fill, Image badge, Text name)
        {
            _fill = fill;
            _badge = badge;
            _name = name;
        }

        public static MenuRankRow Create(Transform parent, float top)
        {
            var go = new GameObject("Rank Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var root = (RectTransform)go.transform;
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(MenuTheme.RankBarWidth, Height);
            root.anchoredPosition = new Vector2(0f, top);

            Image track = CreateImage(root, "Track", MenuTheme.Colors.Border);
            RectTransform tr = track.rectTransform;
            tr.anchorMin = new Vector2(0f, 0.5f);
            tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.sizeDelta = new Vector2(MenuTheme.RankBarWidth, MenuTheme.StatBarHeight);
            tr.anchoredPosition = Vector2.zero;

            Image fill = CreateImage(tr, "Fill", MenuTheme.Colors.Accent);
            RectTransform fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(0f, 1f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.sizeDelta = Vector2.zero;
            fr.anchoredPosition = Vector2.zero;

            float badgeX = MenuTheme.RankBarWidth + MenuTheme.RankBarToBadge;
            Image badge = CreateImage(root, "Badge", Color.white);
            badge.preserveAspect = true;
            RectTransform br = badge.rectTransform;
            br.anchorMin = new Vector2(0f, 0.5f);
            br.anchorMax = new Vector2(0f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.sizeDelta = Vector2.one * MenuTheme.RankBadgeSize;
            br.anchoredPosition = new Vector2(badgeX + MenuTheme.RankBadgeSize * 0.5f, 0f);

            Text name = UIFactory.CreateInlineLabel(root, string.Empty, MenuTheme.RankNameSize,
                Vector2.zero, Height, MenuTheme.Colors.Fg, UIFactory.BoldFont);
            RectTransform nr = name.rectTransform;
            nr.anchorMin = new Vector2(0f, 0.5f);
            nr.anchorMax = new Vector2(0f, 0.5f);
            nr.pivot = new Vector2(0f, 0.5f);
            nr.anchoredPosition = new Vector2(
                badgeX + MenuTheme.RankBadgeSize + MenuTheme.RankBadgeToName, 0f);

            return new MenuRankRow(fr, badge, name);
        }

        public void Show(int points)
        {
            int rank = PilotRanks.IndexFor(points);
            SetRank(rank);
            SetFill(PilotRanks.Progress(points, rank));
        }

        public IEnumerator Play(RankResult result)
        {
            int rank = PilotRanks.IndexFor(result.Before);
            int shown = result.Before;
            Show(shown);

            while (ScreenFade.IsBusy) yield return null;
            yield return Wait(StartDelaySec);

            while (shown < result.After)
            {
                bool top = rank >= PilotRanks.Last;
                int cap = top ? result.After : PilotRanks.All[rank + 1].Points;
                int target = Mathf.Min(result.After, cap);

                float from = PilotRanks.Progress(shown, rank);
                float to = PilotRanks.Progress(target, rank);
                float duration = Mathf.Max(MinFillSec, FullFillSec * (to - from));

                for (float t = 0f; t < duration; t += Mathf.Min(Time.unscaledDeltaTime, MaxStep))
                {
                    SetFill(Mathf.Lerp(from, to, Ease(t / duration)));
                    yield return null;
                }

                SetFill(to);
                shown = target;
                if (top || shown < cap) yield break;

                yield return Wait(PromoteHoldSec);
                rank++;
                SetRank(rank);
                SetFill(PilotRanks.Progress(shown, rank));
                yield return Pop();
            }
        }

        void SetRank(int rank)
        {
            _badge.sprite = PilotRanks.Badge(rank);
            _badge.enabled = _badge.sprite != null;
            _name.text = PilotRanks.All[rank].Name.ToUpperInvariant();
            RectTransform rt = _name.rectTransform;
            rt.sizeDelta = new Vector2(_name.preferredWidth, rt.sizeDelta.y);
        }

        void SetFill(float fraction) =>
            _fill.sizeDelta = new Vector2(MenuTheme.RankBarWidth * Mathf.Clamp01(fraction), 0f);

        IEnumerator Pop()
        {
            Transform badge = _badge.transform;
            for (float t = 0f; t < PopSec; t += Mathf.Min(Time.unscaledDeltaTime, MaxStep))
            {
                badge.localScale = Vector3.one * Mathf.Lerp(PopScale, 1f, Ease(t / PopSec));
                yield return null;
            }
            badge.localScale = Vector3.one;
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        static float Ease(float t) => t * t * (3f - 2f * t);

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
