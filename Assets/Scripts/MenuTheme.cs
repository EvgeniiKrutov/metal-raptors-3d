using UnityEngine;

namespace MetalRaptors
{
    public enum MenuThemeId { Dusk = 0, WW1 = 1, WW2 = 2, ColdWar = 3, Modern = 4 }

    public readonly struct MenuPalette
    {
        public readonly Color Bg;
        public readonly Color Fg;
        public readonly Color Muted;
        public readonly Color Accent;
        public readonly Color Panel;
        public readonly Color Border;

        public MenuPalette(string bg, string fg, string muted, string accent, string panel, string border)
        {
            Bg = Parse(bg);
            Fg = Parse(fg);
            Muted = Parse(muted);
            Accent = Parse(accent);
            Panel = Parse(panel);
            Border = Parse(border);
        }

        static Color Parse(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }

    /// <summary>Palettes and metrics of the main-menu look. See docs/main-menu.md.</summary>
    public static class MenuTheme
    {
        static readonly MenuPalette[] Palettes =
        {
            new MenuPalette("#E7DAE0", "#3A2E44", "#8A7A8C", "#B5687E", "#DAC9D2", "#C7B2BE"),
            new MenuPalette("#E7DEC9", "#4A3B2A", "#9A8A6E", "#8A6B3A", "#DBD0B7", "#C9BC9C"),
            new MenuPalette("#D9D6BE", "#3B4028", "#86876C", "#9E4A3C", "#CBC9AE", "#B9B79A"),
            new MenuPalette("#D9DEE0", "#2C3840", "#7E8A90", "#4E7C8A", "#CAD1D4", "#B6BFC3"),
            new MenuPalette("#EDEFF2", "#1F2933", "#7C8794", "#3B7BB8", "#E0E3E8", "#CCD1D8"),
        };

        public static MenuThemeId Active = MenuThemeId.Dusk;

        public static MenuPalette Colors => Palettes[(int)Active];

        public const float ColumnFraction = 0.4f;
        public const float PadTopFraction = 0.15f;

        // Ragged-left design: the left margin is the edge every screen is composed against,
        // the right one only keeps stretched rows off the screen edge.
        public const float PadLeft = 120f;
        public const float PadRight = 56f;

        public const int TitleSize = 44;
        public const float TitleRowHeight = 46f;
        public const float TitleToBar = 22f;
        public const float BarWidth = 72f;
        public const float BarHeight = 4f;
        public const float BarToList = 34f;

        public const int ItemSize = 30;
        public const float ItemRowHeight = 44f;
        public const float ItemGap = 10f;

        public const int OptionSize = 22;
        public const float OptionRowHeight = 34f;
        public const float OptionGap = 26f;

        public const int CaptionSize = 14;
        public const float CaptionRowHeight = 20f;
        public const float CaptionToContent = 12f;
        public const float SectionGap = 32f;
        public const float TagGap = 16f;

        public const int DescriptionSize = 20;
        public const float DescriptionWidth = 940f;
        public const float DescriptionRowHeight = 124f;
        public const float DescriptionLineSpacing = 1.35f;
        public const float DescriptionToCards = 48f;

        public static readonly Color CardFace = Color.white;

        public const float CardSize = 360f;
        public const float CardGap = 40f;
        public const float CardBorder = 4f;
        public const float CardPad = 28f;
        public const int CardTitleSize = 25;
        public const float CardTitleRowHeight = 32f;
        public const int CardYearsSize = 15;
        public const float CardYearsRowHeight = 20f;
        public const float CardTitleToYears = 2f;

        public static float ListTop =>
            -(TitleRowHeight + TitleToBar + BarHeight + BarToList);
    }
}
