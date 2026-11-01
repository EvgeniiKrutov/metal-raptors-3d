using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class LoadingScreen : MonoBehaviour
    {
        const string Folder = "Wallpapers/";
        const string LoadingCaption = "Loading";
        const string HiddenOpen = "<color=#00000000>";
        const string HiddenClose = "</color>";
        const int DotCount = 3;
        const float DotSec = 0.4f;
        const float MinShowSec = 2f;

        const int TitleSize = 104;
        const float TitleAnchorY = 0.7f;
        const float TitleToRule = 78f;
        const int PromptSize = 34;
        const float PromptBottom = 92f;
        const float TextWidth = 1600f;

        const float BottomShadeHeight = 0.4f;
        const float BottomShadeAlpha = 0.85f;
        const float TopShadeHeight = 0.55f;
        const float TopShadeAlpha = 0.6f;
        const int ShadeSteps = 64;

        static readonly string[] Names =
        {
            "arena_duel_a_4k",
            "flanders_midday_a_4k",
            "verdun_escape_a_4k",
            "verdun_escape_c_4k",
            "verdun_evening_a_4k",
            "verdun_midday_a_4k",
        };

        static readonly Color TextColor = Color.white;
        static readonly Color LoadingColor = new Color(1f, 1f, 1f, 0.8f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.9f);
        static readonly Vector2 ShadowOffset = new Vector2(2f, -2f);
        static readonly Vector2 RuleSize = new Vector2(120f, 5f);

        static int _last = -1;

        RawImage _wallpaper;
        AspectRatioFitter _fit;
        GameObject _topShade;
        Text _title;
        Image _rule;
        Text _loading;
        CanvasGroup _prompt;
        float _age;
        int _dots = -1;
        float _shownAt;

        public bool Visible => gameObject.activeSelf;

        public bool Held => Time.realtimeSinceStartup - _shownAt >= MinShowSec;

        public static bool Covers(string scene) =>
            scene != SceneNames.MainMenu && scene != SceneNames.Garage;

        public static LoadingScreen Build(Transform parent)
        {
            var root = new GameObject("Loading Screen", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.transform.SetSiblingIndex(0);
            Stretch((RectTransform)root.transform);

            var screen = root.AddComponent<LoadingScreen>();
            screen.Compose();
            root.SetActive(false);
            return screen;
        }

        void Compose()
        {
            UIFactory.CreateBackground(transform, Color.black);

            var go = new GameObject("Wallpaper", typeof(RawImage), typeof(AspectRatioFitter));
            go.transform.SetParent(transform, false);
            _wallpaper = go.GetComponent<RawImage>();
            _wallpaper.raycastTarget = false;
            _fit = go.GetComponent<AspectRatioFitter>();
            _fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            Texture2D shade = ShadeTexture();
            BuildShade(shade, false, BottomShadeHeight, BottomShadeAlpha);
            _topShade = BuildShade(shade, true, TopShadeHeight, TopShadeAlpha);

            float scale = MenuTheme.TextScale;
            _title = BuildText("Title", Mathf.RoundToInt(TitleSize * scale), UIFactory.BoldFont,
                TextColor, new Vector2(0.5f, TitleAnchorY), 0f);

            var rule = new GameObject("Rule", typeof(Image));
            rule.transform.SetParent(transform, false);
            _rule = rule.GetComponent<Image>();
            _rule.color = MenuTheme.Colors.Accent;
            _rule.raycastTarget = false;
            var ruleRt = _rule.rectTransform;
            ruleRt.anchorMin = new Vector2(0.5f, TitleAnchorY);
            ruleRt.anchorMax = ruleRt.anchorMin;
            ruleRt.sizeDelta = RuleSize * scale;
            ruleRt.anchoredPosition = new Vector2(0f, -TitleToRule * scale);
            Shade(rule);

            _loading = BuildText("Loading", Mathf.RoundToInt(PromptSize * scale), UIFactory.MediumFont,
                LoadingColor, new Vector2(0.5f, 0f), PromptBottom + MenuTheme.SafeBottom);
        }

        public void Show(string title)
        {
            HidePrompt();
            Release();

            Texture2D texture = Pick();
            _wallpaper.texture = texture;
            _wallpaper.enabled = texture != null;
            if (texture != null) _fit.aspectRatio = (float)texture.width / texture.height;

            bool titled = !string.IsNullOrEmpty(title);
            _title.text = titled ? title : string.Empty;
            _title.gameObject.SetActive(titled);
            _rule.gameObject.SetActive(titled);
            _topShade.SetActive(titled);

            _age = 0f;
            _dots = -1;
            _shownAt = Time.realtimeSinceStartup;
            _loading.gameObject.SetActive(true);
            TickDots();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            HidePrompt();
            gameObject.SetActive(false);
            Release();
        }

        public IEnumerator AwaitContinue()
        {
            while (!Held) yield return null;

            _loading.gameObject.SetActive(false);
            HidePrompt();

            _prompt = LevelBriefing.BuildPrompt(transform, 0f,
                Mathf.RoundToInt(PromptSize * MenuTheme.TextScale), TextColor,
                MenuTheme.Colors.Accent, out Image caret);
            Shade(_prompt.GetComponentInChildren<Text>().gameObject);
            Shade(caret.gameObject);

            var rt = (RectTransform)_prompt.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = rt.anchorMin;
            rt.anchoredPosition = new Vector2(0f, PromptBottom + MenuTheme.SafeBottom);

            CanvasGroup prompt = _prompt;
            for (float age = 0f; ; age += Time.unscaledDeltaTime)
            {
                if (prompt == null) yield break;

                prompt.alpha = Mathf.Clamp01(age / LevelBriefing.PromptFadeSec);
                caret.enabled = Mathf.Repeat(age, 2f * LevelBriefing.CaretBlinkSec)
                                < LevelBriefing.CaretBlinkSec;
                yield return null;
                if (MenuInput.ReadAnyKey()) break;
            }
        }

        void Update()
        {
            if (!_loading.gameObject.activeSelf) return;

            _age += Time.unscaledDeltaTime;
            TickDots();
        }

        void TickDots()
        {
            int dots = (int)(_age / DotSec) % (DotCount + 1);
            if (dots == _dots) return;

            _dots = dots;
            _loading.text = LoadingCaption + new string('.', dots)
                            + HiddenOpen + new string('.', DotCount - dots) + HiddenClose;
        }

        void HidePrompt()
        {
            if (_prompt == null) return;

            Destroy(_prompt.gameObject);
            _prompt = null;
        }

        void Release()
        {
            Texture texture = _wallpaper.texture;
            _wallpaper.texture = null;
            if (texture != null) Resources.UnloadAsset(texture);
        }

        static Texture2D Pick()
        {
            if (Names.Length == 0) return null;

            int index = _last < 0 || Names.Length == 1
                ? Random.Range(0, Names.Length)
                : (_last + 1 + Random.Range(0, Names.Length - 1)) % Names.Length;
            _last = index;
            return Resources.Load<Texture2D>(Folder + Names[index]);
        }

        Text BuildText(string name, int size, Font font, Color color, Vector2 anchor, float y)
        {
            Text text = UIFactory.CreateText(transform, string.Empty, size, Vector2.zero,
                new Vector2(TextWidth, size * 1.6f));
            text.name = name;
            text.font = font;
            text.color = color;

            var rt = text.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = new Vector2(0f, y);
            Shade(text.gameObject);
            return text;
        }

        GameObject BuildShade(Texture2D texture, bool top, float height, float alpha)
        {
            var go = new GameObject(top ? "Top Shade" : "Bottom Shade", typeof(RawImage));
            go.transform.SetParent(transform, false);

            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.color = new Color(0f, 0f, 0f, alpha);
            image.raycastTarget = false;
            image.uvRect = top ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, top ? 1f - height : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        static Texture2D ShadeTexture()
        {
            var texture = new Texture2D(1, ShadeSteps, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int y = 0; y < ShadeSteps; y++)
            {
                float t = 1f - y / (ShadeSteps - 1f);
                texture.SetPixel(0, y, new Color(1f, 1f, 1f, t * t * (3f - 2f * t)));
            }

            texture.Apply();
            return texture;
        }

        static void Shade(GameObject target)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = ShadowOffset;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
