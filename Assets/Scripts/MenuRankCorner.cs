using UnityEngine;

namespace MetalRaptors
{
    public class MenuRankCorner
    {
        public GameObject Root { get; }
        public MenuAvatarView Avatar { get; }

        MenuRankCorner(GameObject root, MenuAvatarView avatar)
        {
            Root = root;
            Avatar = avatar;
        }

        public static MenuRankCorner Create(Transform parent, int rank)
        {
            var go = new GameObject("Rank Corner", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var root = (RectTransform)go.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.sizeDelta = new Vector2(0f, MenuTheme.CornerAvatarSize);
            root.anchoredPosition = new Vector2(MenuTheme.PageInsetLeft,
                MenuTheme.SafeBottom + MenuTheme.CornerBottom);

            MenuAvatarView avatar = MenuAvatarView.Create(root, MenuTheme.CornerAvatarSize,
                PilotRanks.Badge(rank));
            PlaceLeft(avatar.RectTransform, 0f);

            return new MenuRankCorner(go, avatar);
        }

        static void PlaceLeft(RectTransform rt, float fromLeft)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(fromLeft, 0f);
        }

        public void SetActive(bool active) => Root.SetActive(active);
    }
}
