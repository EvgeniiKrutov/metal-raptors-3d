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
        public static readonly Color Face = new Color(0.07f, 0.10f, 0.13f, 0.42f);
        public static readonly Color Amber = new Color(1f, 0.72f, 0.2f, 1f);
        public static readonly Color Alarm = new Color(0.92f, 0.27f, 0.22f, 1f);
        public static readonly Color Boss = new Color(0.84f, 0.24f, 0.20f, 1f);

        public static readonly float MarginSide = Touch ? 44f : 100f;
        public static readonly float MarginTop = Touch ? 28f : 41f;
        public static readonly float MarginBottom = Touch ? 26f : 15f;
        public static readonly float DialMarginSide = Touch ? 44f : 40f;
        public static readonly float DialMarginTop = Touch ? 28f : 40f;

        public static readonly float BarHeight = Touch ? 54f : 38f;

        public static readonly float DialSize = Touch ? 172f : 138f;
        public static readonly float DialRim = Touch ? 4f : 3f;

        public static readonly float SkillSize = Touch ? 152f : 69f;
        public static readonly float SkillRim = Touch ? 4f : 2f;
        public static readonly float SkillGap = Touch ? 30f : 22f;
        public static readonly float SkillSpacing = Touch ? 30f : 22f;
        public static readonly float SkillMid = Touch ? 56f : 45f;
        public static readonly float SkillHitPad = Touch ? 6f : 0f;
        public static readonly float PressScale = 0.93f;

        public static readonly float FireSize = 202f;
        public static readonly float FireInsetLeft = 268f;
        public static readonly float FireInsetBottom = 204f;

        public static readonly float SlimWidth = Touch ? 600f : 520f;
        public static readonly float SlimHeight = Touch ? 16f : 12f;
        public static readonly float SlimLabelRow = Touch ? 40f : 34f;
        public static readonly int SlimLabelSize = Touch ? 26 : 22;

        public static readonly float StickThumbSize = 150f;
        public static readonly float StickThumbStroke = 4f;
        public static readonly float StickClampRadius = 210f;
        public static readonly float StickInsetRight = 260f;
        public static readonly float StickInsetBottom = 250f;
        public static readonly Vector2 StickGrabSize = new Vector2(720f, 620f);
        public static readonly float StickDeadzone = 0.20f;
        public static readonly float StickReturn = 0.12f;
        public static readonly Color Stick = new Color(0.88f, 0.89f, 0.91f, 0.5f);

        public static readonly float ArrowOrbit = 110f;
        public static readonly float ArrowArm = 16f;
        public static readonly float ArrowStroke = 1f;

        public static readonly int HintSize = Touch ? 34 : 28;
        public static readonly float HintRowHeight = Touch ? 56f : 50f;

        public static float DialLeft => MenuTheme.SafeLeft + DialMarginSide;
        public static float DialTop => MenuTheme.SafeTop + DialMarginTop;
        public static float ColumnRight => MenuTheme.SafeRight + MarginSide;
        public static float ColumnTop => MenuTheme.SafeTop + MarginTop;
        public static float HintBottom => MenuTheme.SafeBottom + MarginBottom;

        public static bool IsTouch => Touch;

        public static Vector2 DialCentre =>
            new Vector2(DialLeft + DialSize * 0.5f, -(DialTop + DialSize * 0.5f));

        public static Vector2 SpeedDialCentre =>
            new Vector2(-(MenuTheme.SafeRight + DialMarginSide + DialSize * 0.5f),
                -(DialTop + DialSize * 0.5f));

        public static Vector2 FireCentre =>
            new Vector2(MenuTheme.SafeLeft + FireInsetLeft, MenuTheme.SafeBottom + FireInsetBottom);

        public static Vector2 SkillAnchor => Touch ? Vector2.zero : new Vector2(0f, 1f);

        public static Vector2 SkillHub => Touch ? FireCentre : DialCentre;

        public static float SkillTurn => Touch ? 1f : -1f;

        public static float SkillOrbit =>
            (Touch ? FireSize : DialSize) * 0.5f + SkillGap + SkillSize * 0.5f;

        public static float SkillStep =>
            2f * Mathf.Asin((SkillSize + SkillSpacing) * 0.5f / SkillOrbit) * Mathf.Rad2Deg;

        public static float ClusterRight =>
            DialLeft + (Touch ? DialSize : DialSize * 0.5f + SkillOrbit + SkillSize * 0.5f);

        public static float HintSideInset =>
            Mathf.Max(MenuTheme.SafeLeft, MenuTheme.SafeRight) + MarginSide;
    }
}
