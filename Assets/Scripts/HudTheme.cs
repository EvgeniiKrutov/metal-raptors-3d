using UnityEngine;

namespace MetalRaptors
{
    public static class HudTheme
    {
        static readonly bool Touch = MenuInput.IsTouchPlatform;

        public static readonly Color Fill = Color.white;
        public static readonly Color Idle = new Color(0.88f, 0.89f, 0.91f, 0.85f);
        public static readonly Color Charge = new Color(0.88f, 0.89f, 0.91f, 0.42f);
        public static readonly Color Track = new Color(0.88f, 0.89f, 0.91f, 0.28f);
        public static readonly Color Ink = new Color(0.11f, 0.12f, 0.14f, 1f);

        public static readonly float MarginSide = Touch ? 44f : 100f;
        public static readonly float MarginTop = Touch ? 28f : 41f;
        public static readonly float MarginBottom = Touch ? 26f : 15f;

        public static readonly float BarWidth = Touch ? 460f : 400f;
        public static readonly float BarHeight = Touch ? 54f : 38f;
        public static readonly float BarRadius = Touch ? 9f : 6f;
        public static readonly int BarTextSize = Touch ? 28 : 24;

        public static readonly float BarToColumn = Touch ? 22f : 8f;
        public static readonly float SquareSize = Touch ? 132f : 56f;
        public static readonly float SquareGap = Touch ? 20f : 8f;
        public static readonly float SquareOutline = Touch ? 4f : 2f;
        public static readonly float SquareRadius = Touch ? 16f : 8f;
        public static readonly float SquareHitPad = Touch ? 10f : 0f;
        public static readonly int SquareLabelSize = Touch ? 22 : 26;

        public static readonly int HintSize = Touch ? 34 : 28;
        public static readonly float HintRowHeight = Touch ? 56f : 50f;

        public static float ColumnLeft => MenuTheme.SafeLeft + MarginSide;
        public static float ColumnRight => MenuTheme.SafeRight + MarginSide;
        public static float ColumnTop => MenuTheme.SafeTop + MarginTop;
        public static float HintBottom => MenuTheme.SafeBottom + MarginBottom;

        public static bool IsTouch => Touch;

        public static string Label(string key, string word) => Touch ? word : key;

        public static float SquarePitch => SquareSize + SquareGap;

        public static float WedgeInset => SquareOutline * 2f;

        public static float HintSideInset =>
            Mathf.Max(MenuTheme.SafeLeft, MenuTheme.SafeRight) + MarginSide;
    }
}
