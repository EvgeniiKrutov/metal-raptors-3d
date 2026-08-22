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

    public readonly struct CardMetrics
    {
        public readonly float Size;
        public readonly float Gap;
        public readonly float Pad;
        public readonly float Border;
        public readonly int TitleSize;
        public readonly float TitleRowHeight;
        public readonly float ArtBottom;

        public CardMetrics(int visible, int titleLines, float top)
        {
            Size = MenuTheme.RowCardSize(visible, top);

            float k = Size / MenuTheme.CardSizeMax;
            Gap = MenuTheme.CardGap * k;
            Pad = MenuTheme.CardPad * k;
            Border = MenuTheme.CardBorder * k;
            TitleSize = Mathf.RoundToInt(MenuTheme.CardTitleSize * k);
            TitleRowHeight = (titleLines > 1 ? titleLines * MenuTheme.CardTitleLineHeight
                                             : MenuTheme.CardTitleRowHeight) * k;
            ArtBottom = Pad + TitleRowHeight + MenuTheme.CardTitleToArt * k;
        }

        public float Pitch => Size + Gap;
    }

    public readonly struct SafeInsets
    {
        public readonly float Left;
        public readonly float Right;
        public readonly float Top;
        public readonly float Bottom;

        public SafeInsets(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }
    }

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

        public const float TouchTextScale = 1.4f;
        public const float TouchArrowScale = 1.4f;
        public const float TouchArrowPad = 20f;
        public const float PadTopFractionDesk = 0.15f;
        public const float PadTopFractionTouch = 0.10f;

        static readonly bool Touch = MenuInput.IsTouchPlatform;

        public static readonly float TextScale = Touch ? TouchTextScale : 1f;
        public static readonly float WidthScale = 1f + (TextScale - 1f) * 0.5f;
        public static readonly float ArrowScale = Touch ? TouchArrowScale : 1f;
        public static readonly float ArrowPad = Touch ? TouchArrowPad : 0f;
        public static readonly float PadTopFraction = Touch ? PadTopFractionTouch : PadTopFractionDesk;

        public const float PadLeft = 120f;
        public const float PadRight = 56f;

        public static float SafeLeft { get; private set; }
        public static float SafeRight { get; private set; }
        public static float SafeTop { get; private set; }
        public static float SafeBottom { get; private set; }

        public const int TitleSize = 44;
        public const float TitleRowHeight = 46f;
        public const float TitleToBar = 22f;
        public const float BarWidth = 72f;
        public const float BarHeight = 4f;
        public const float BarToList = TitleToBar;

        public const int ItemSizeBase = 30;
        public const float ItemRowHeightBase = 44f;
        public const float ItemGapBase = 10f;

        public static int ItemSize => Scaled(ItemSizeBase);
        public static float ItemRowHeight => ItemRowHeightBase * TextScale;
        public static float ItemGap => ItemGapBase * TextScale;

        public const int OptionSizeBase = 22;
        public const float OptionRowHeightBase = 34f;
        public const float OptionGapBase = 26f;

        public static int OptionSize => Scaled(OptionSizeBase);
        public static float OptionRowHeight => OptionRowHeightBase * TextScale;
        public static float OptionGap => OptionGapBase * TextScale;

        public const float SelectorLabelWidthBase = 190f;
        public const float SelectorValueWidthBase = 190f;
        public const float SelectorArrowGapBase = 22f;
        static readonly Vector2 SelectorArrowSizeBase = new Vector2(18f, 20f);

        public static float SelectorLabelWidth => SelectorLabelWidthBase * WidthScale;
        public static float SelectorValueWidth => SelectorValueWidthBase * WidthScale;
        public static float SelectorArrowGap => SelectorArrowGapBase * WidthScale;
        public static Vector2 SelectorArrowSize => SelectorArrowSizeBase * ArrowScale;

        public static float SelectorRowWidth =>
            SelectorLabelWidth + 2f * (SelectorArrowSize.x + SelectorArrowGap) + SelectorValueWidth;

        static int Scaled(int size) => Mathf.RoundToInt(size * TextScale);

        public const int CaptionSize = 14;
        public const float CaptionRowHeight = 20f;
        public const float CaptionToContent = 12f;
        public const float SectionGap = 32f;
        public const float TagGap = 16f;

        public const float StatBarWidth = 460f;
        public const float StatBarHeight = 10f;
        public const int StatCaptionSize = 20;
        public const float StatCaptionRowHeight = 26f;
        public const float StatCaptionToValue = 8f;
        public const int StatValueSize = 22;
        public const float StatRowGap = 18f;

        public const int BadgeSize = 15;
        public const float BadgeHeight = 28f;
        public const float BadgePadX = 14f;
        public const float BadgeValueGap = 16f;
        public const float BadgeToContent = 16f;

        public const float GaragePadLeftBase = 200f;
        public const float GarageArrowToColumn = 80f;

        public static float GaragePadLeft => Mathf.Max(GaragePadLeftBase,
            SafeLeft + GarageArrowInset + GarageArrowSize.x + GarageArrowToColumn);

        static readonly Vector2 GarageArrowSizeBase = new Vector2(30f, 38f);

        public static Vector2 GarageArrowSize => GarageArrowSizeBase * ArrowScale;
        public const float GarageArrowInset = 44f;
        public const float ArrowToCards = 46f;
        public const float GarageDescriptionWidth = 1080f;
        public const float GarageDescriptionBottom = 124f;
        public const float GarageDescriptionRowHeight = 96f;

        public const int DescriptionSize = 20;
        public const float DescriptionWidth = 940f;
        public const float DescriptionRowHeight = 124f;
        public const float DescriptionLineSpacing = 1.35f;
        public const float DescriptionToCards = 48f;

        public static readonly Color CardFace = Color.white;
        public static readonly Color CardDone = new Color32(0x5F, 0x91, 0x59, 0xFF);

        public const float CardSizeMax = 360f;
        public const float CardSizeMin = 280f;
        public const float CardGap = 40f;
        public const float CardBorder = 4f;
        public const float CardPad = 28f;
        public const int CardTitleSize = 25;
        public const float CardTitleRowHeight = 32f;
        public const int RowCards = 4;

        public const float CardTitleToArt = 14f;
        public const float CardTitleLineHeight = 38f;
        public const float CardBottomMargin = 48f;

        const float CardGapRatio = CardGap / CardSizeMax;

        static float _canvasWidth = 1920f;
        static float _canvasHeight = 1080f;

        public static void Fit(float canvasWidth, float canvasHeight, SafeInsets safe)
        {
            _canvasWidth = canvasWidth;
            _canvasHeight = canvasHeight;
            SafeLeft = safe.Left;
            SafeRight = safe.Right;
            SafeTop = safe.Top;
            SafeBottom = safe.Bottom;
        }

        public static float RowArrowLane => GarageArrowInset + GarageArrowSize.x + ArrowToCards;

        public static float PageInsetLeft => SafeLeft + Mathf.Max(PadLeft, RowArrowLane);
        public static float PageInsetRight => SafeRight + Mathf.Max(PadRight, RowArrowLane);

        public static float RowWidth => _canvasWidth - PageInsetLeft - PageInsetRight;

        public static float CardSize => Mathf.Clamp(
            (_canvasWidth - 2f * PadLeft - (RowCards - 1) * CardGap) / RowCards,
            CardSizeMin, CardSizeMax);

        public static float RowCardSize(int visible, float top)
        {
            if (visible <= 0) return CardSizeMax;

            float byWidth = RowWidth / (visible + (visible - 1) * CardGapRatio);
            float byHeight = _canvasHeight * (1f - PadTopFraction) + top - CardBottomMargin;
            return Mathf.Max(CardSizeMin, Mathf.Min(byWidth, byHeight));
        }

        public const int LevelVisibleCards = RowCards;
        public const float LevelRowSlide = 0.18f;

        public const int LevelDateSize = 18;
        public const float LevelDateRowHeight = 24f;
        public const float LevelDateToBrief = 12f;
        public const float LevelBriefRowHeight = 96f;

        public static float LevelCardsTop =>
            ListTop - LevelDateRowHeight - LevelDateToBrief
            - LevelBriefRowHeight - DescriptionToCards;

        public static float ListTop =>
            -(TitleRowHeight + TitleToBar + BarHeight + BarToList);
    }
}
