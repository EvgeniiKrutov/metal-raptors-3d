using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MetalRaptors
{
    public static class UIFactory
    {
        static Font _regular;
        static Font _medium;
        static Font _bold;

        public static Font DefaultFont => RegularFont;

        public static Font RegularFont
        {
            get
            {
                if (_regular == null) _regular = LoadFont("Poppins-Regular");
                return _regular;
            }
        }

        public static Font MediumFont
        {
            get
            {
                if (_medium == null) _medium = LoadFont("Poppins-Medium");
                return _medium;
            }
        }

        public static Font BoldFont
        {
            get
            {
                if (_bold == null) _bold = LoadFont("Poppins-Bold");
                return _bold;
            }
        }

        public static Font FontFor(FontStyle style) =>
            style == FontStyle.Bold || style == FontStyle.BoldAndItalic ? BoldFont : RegularFont;

        static Font LoadFont(string name)
        {
            var font = Resources.Load<Font>(name);
            if (font != null) return font;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public static Canvas CreateCanvas(string name = "UI Canvas")
        {
            EnsureEventSystem();

            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var es = new GameObject("EventSystem", typeof(EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static Image CreateBackground(Transform parent, Color color)
        {
            var go = new GameObject("Background", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        public static Text CreateText(Transform parent, string content, int fontSize, Vector2 anchoredPos,
            Vector2 size, TextAnchor alignment = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = FontFor(style);
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return text;
        }

        public static Text CreateLabel(Transform parent, string content, int fontSize, float top,
            float rowHeight, Color color, Font font)
        {
            Text text = NewLabel(parent, content, fontSize, color, font, TextAnchor.MiddleLeft);

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, rowHeight);
            rt.anchoredPosition = new Vector2(0f, top);
            return text;
        }

        public static Text CreateBottomLabel(Transform parent, string content, int fontSize, float bottom,
            float rowHeight, float padSide, Color color, Font font)
        {
            Text text = NewLabel(parent, content, fontSize, color, font, TextAnchor.MiddleLeft);

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-2f * padSide, rowHeight);
            rt.anchoredPosition = new Vector2(0f, bottom);
            return text;
        }

        public static Text CreateParagraph(Transform parent, string content, int fontSize, float top,
            float width, float rowHeight, float lineSpacing, Color color, Font font)
        {
            Text text = NewLabel(parent, content, fontSize, color, font, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.lineSpacing = lineSpacing;

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, rowHeight);
            rt.anchoredPosition = new Vector2(0f, top);
            return text;
        }

        public static Text CreateCenteredParagraph(Transform parent, string content, int fontSize,
            float bottom, float width, float rowHeight, float lineSpacing, Color color, Font font)
        {
            Text text = NewLabel(parent, content, fontSize, color, font, TextAnchor.LowerCenter);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.lineSpacing = lineSpacing;

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(width, rowHeight);
            rt.anchoredPosition = new Vector2(0f, bottom);
            return text;
        }

        public static Text CreateInlineLabel(Transform parent, string content, int fontSize, Vector2 anchoredPos,
            float rowHeight, Color color, Font font)
        {
            Text text = NewLabel(parent, content, fontSize, color, font, TextAnchor.MiddleLeft);

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(text.preferredWidth, rowHeight);
            rt.anchoredPosition = anchoredPos;
            return text;
        }

        static Text NewLabel(Transform parent, string content, int fontSize, Color color, Font font,
            TextAnchor alignment)
        {
            var go = new GameObject($"Label ({content})", typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Image CreateTriangle(Transform parent, bool pointsLeft, Vector2 size,
            Vector2 anchoredPos, Color color)
        {
            var go = new GameObject(pointsLeft ? "Triangle (left)" : "Triangle (right)", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = TriangleSprite(pointsLeft);
            img.color = color;

            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return img;
        }

        static Sprite _triangleLeft;
        static Sprite _triangleRight;

        static Sprite TriangleSprite(bool pointsLeft)
        {
            if (pointsLeft && _triangleLeft != null) return _triangleLeft;
            if (!pointsLeft && _triangleRight != null) return _triangleRight;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = pointsLeft ? "Triangle Left" : "Triangle Right",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                float span = 1f - Mathf.Abs(2f * v - 1f);
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    if (pointsLeft) u = 1f - u;

                    float alpha = Mathf.Clamp01((span - u) * size * 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            sprite.hideFlags = HideFlags.HideAndDontSave;

            if (pointsLeft) _triangleLeft = sprite;
            else _triangleRight = sprite;
            return sprite;
        }

        public static Image CreateRule(Transform parent, float top, Vector2 size, Color color)
        {
            var go = new GameObject("Rule", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0f, top);
            return img;
        }

        public static Button CreateButton(Transform parent, string label, Vector2 anchoredPos, Action onClick,
            Vector2? size = null, bool interactable = true, int fontSize = 34)
        {
            var s = size ?? new Vector2(460, 92);

            var go = new GameObject($"Button ({label})", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            var rt = img.rectTransform;
            rt.sizeDelta = s;
            rt.anchoredPosition = anchoredPos;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            button.interactable = interactable;

            var colors = button.colors;
            colors.normalColor = new Color(0.13f, 0.14f, 0.17f, 1f);
            colors.highlightedColor = new Color(0.85f, 0.24f, 0.16f, 1f);
            colors.pressedColor = new Color(0.60f, 0.16f, 0.10f, 1f);
            colors.selectedColor = new Color(0.20f, 0.22f, 0.26f, 1f);
            colors.disabledColor = new Color(0.10f, 0.10f, 0.11f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            img.color = colors.normalColor;

            if (onClick != null) button.onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var text = labelGo.GetComponent<Text>();
            text.font = BoldFont;
            text.text = label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = interactable ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            text.raycastTarget = false;

            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return button;
        }

        public static Transform CreatePlaceholderProp(PrimitiveType type, Vector3 position, Color color, float scale = 2f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "Placeholder Prop";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", color);
                renderer.sharedMaterial = mat;
            }

            return go.transform;
        }

        public static GameObject CreatePrimitive3D(PrimitiveType type, Vector3 position, Vector3 scale,
            Color color, bool emissive = false, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.position = position;
            go.transform.localScale = scale;

            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }

            var renderer = go.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", color);
                if (emissive)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", color * 2f);
                }
                renderer.sharedMaterial = mat;
            }

            return go;
        }

        public static void MakeTransparent(Material mat)
        {
            if (mat == null) return;
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
