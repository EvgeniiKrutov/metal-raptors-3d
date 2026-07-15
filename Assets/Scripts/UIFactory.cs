using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// Helpers for building screen-space uGUI (and a simple 3D placeholder prop) at
    /// runtime, so every scene's UI is created and wired entirely in code — no manual
    /// editor setup required. Uses the built-in legacy font so there are no font-asset
    /// dependencies to import.
    /// </summary>
    public static class UIFactory
    {
        static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        /// <summary>Creates a full-screen scaling Canvas and guarantees an EventSystem exists.</summary>
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

        /// <summary>
        /// Creates an EventSystem driven by the new Input System's UI module (this project
        /// has the Input System package active, so the legacy StandaloneInputModule would
        /// not work). Default UI input actions are assigned so clicks work immediately.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var es = new GameObject("EventSystem", typeof(EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        /// <summary>Full-screen solid-color background image under the given parent.</summary>
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
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
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

        public static Button CreateButton(Transform parent, string label, Vector2 anchoredPos, Action onClick,
            Vector2? size = null, bool interactable = true)
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

            // Label as a child stretched to fill the button.
            var labelGo = new GameObject("Label", typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var text = labelGo.GetComponent<Text>();
            text.font = DefaultFont;
            text.text = label;
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
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

        /// <summary>
        /// Spawns a lit 3D primitive (the "3D object" of our 2.5D scenes) using a URP
        /// material so it doesn't render magenta, and returns its transform so the caller
        /// can spin it. Purely a visual placeholder to prove the scene is set up.
        /// </summary>
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

        /// <summary>
        /// General-purpose lit 3D primitive with a URP material. Optionally makes it glow
        /// (emissive) and/or strips its collider for purely decorative objects.
        /// </summary>
        public static GameObject CreatePrimitive3D(PrimitiveType type, Vector3 position, Vector3 scale,
            Color color, bool emissive = false, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.position = position;
            go.transform.localScale = scale;

            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.Destroy(col);
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
    }
}
