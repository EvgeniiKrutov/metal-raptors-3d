using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class ProfilePage : IMenuFocusGroup
    {
        const string Title = "PROFILE";

        readonly GameObject _root;
        readonly MenuPanel _panel;

        public ProfilePage(Transform parent, Action onBack)
        {
            Transform screen = MenuLayout.CreateScreen(parent, "Profile Screen");
            _root = screen.gameObject;

            Transform page = MenuLayout.CreatePage(screen, "Profile Page", MenuTheme.ColumnFraction);
            MenuLayout.BuildTitle(page, Title);

            int points = PilotProfile.Points;
            int rank = PilotRanks.IndexFor(points);
            bool top = rank >= PilotRanks.Last;

            float y = MenuTheme.ListTop;
            UIFactory.CreateLabel(page, PilotRanks.All[rank].Name.ToUpperInvariant(),
                MenuTheme.ProfileRankSize, y, MenuTheme.ProfileRankRowHeight, MenuTheme.Colors.Fg,
                UIFactory.BoldFont);
            y -= MenuTheme.ProfileRankRowHeight + MenuTheme.ProfileRankToBar;

            string caption = top ? "highest rank" : $"next: {PilotRanks.All[rank + 1].Name}";
            MenuStatRow bar = MenuStatRow.CreateBar(page, caption, y);
            bar.SetFill(PilotRanks.Progress(points, rank));

            if (!top)
            {
                float barTop = y - (MenuTheme.StatCaptionRowHeight + MenuTheme.StatCaptionToValue);
                float valueTop = barTop + 0.5f * (MenuTheme.StatCaptionRowHeight - MenuTheme.StatBarHeight);
                UIFactory.CreateInlineLabel(page,
                    $"{Format(points)} / {Format(PilotRanks.All[rank + 1].Points)}",
                    MenuTheme.StatValueSize,
                    new Vector2(MenuTheme.StatBarWidth + MenuTheme.ProfileBarValueGap, valueTop),
                    MenuTheme.StatCaptionRowHeight, MenuTheme.Colors.Fg, UIFactory.MediumFont);
            }
            y -= MenuStatRow.BarHeight + MenuTheme.ProfileBarToBack;

            _panel = new MenuPanel(page, "Profile Panel", y);
            _panel.AddNav("back", onBack);

            Transform art = MenuLayout.CreateRegion(screen, "Profile Badge", MenuTheme.ColumnFraction, 1f, 0f);
            var badgeGo = new GameObject("Badge", typeof(Image));
            badgeGo.transform.SetParent(art, false);
            var badge = badgeGo.GetComponent<Image>();
            badge.sprite = PilotRanks.Badge(rank);
            badge.enabled = badge.sprite != null;
            badge.preserveAspect = true;
            badge.raycastTarget = false;

            RectTransform rt = badge.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = Vector2.one * MenuTheme.ProfileBadgeSize;
            rt.anchoredPosition = new Vector2(0f, MenuTheme.ListTop);

            _root.SetActive(false);
        }

        static string Format(int points) => points.ToString("N0", CultureInfo.InvariantCulture);

        public void SetActive(bool active)
        {
            _root.SetActive(active);
            if (active) _panel.FocusFirst();
        }

        public void MoveFocus(int delta) => _panel.MoveFocus(delta);

        public void Adjust(int delta) => _panel.Adjust(delta);

        public void ActivateFocused() => _panel.ActivateFocused();
    }
}
