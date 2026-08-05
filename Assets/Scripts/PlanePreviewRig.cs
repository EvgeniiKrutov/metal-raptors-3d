using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MetalRaptors
{
    public struct PlanePreviewFraming
    {
        public float fieldOfView;
        public float viewYawDeg;
        public float viewPitchDeg;
        public float fillWidth;
        public float fillHeight;
        public float verticalMarginFraction;
        public float regionLeftFraction;
        public float regionBottomFraction;
    }

    public class PlanePreviewRig
    {
        public static readonly Vector3 Origin = new Vector3(0f, 5000f, 0f);

        readonly PlanePreviewFraming _framing;
        readonly Canvas _canvas;
        readonly RawImage _image;
        readonly Camera _camera;

        RenderTexture _texture;
        float _planeSize;

        public float VisibleHeight { get; private set; }

        public GameObject Region => _image != null ? _image.gameObject : null;

        public RectTransform RegionRect => _image != null ? _image.rectTransform : null;

        public PlanePreviewRig(Transform canvas, Transform owner, PlanePreviewFraming framing, float planeSize)
        {
            _framing = framing;
            _planeSize = planeSize;
            _canvas = canvas.GetComponentInParent<Canvas>();
            _image = CreateRegionImage(canvas, framing.regionLeftFraction, framing.regionBottomFraction);

            owner.position = Origin;
            _camera = CreateCamera(owner, framing);
        }

        public void SetPlaneSize(float planeSize)
        {
            if (Mathf.Approximately(_planeSize, planeSize)) return;

            _planeSize = planeSize;
            if (_texture != null) FrameCamera(_texture.width, _texture.height);
        }

        public void Update()
        {
            if (_canvas == null || _image == null) return;

            Rect rect = _image.rectTransform.rect;
            int width = Mathf.Max(16, Mathf.RoundToInt(rect.width * _canvas.scaleFactor));
            int height = Mathf.Max(16, Mathf.RoundToInt(rect.height * _canvas.scaleFactor));
            if (_texture != null && _texture.width == width && _texture.height == height) return;

            Release();
            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
            {
                name = "Plane Preview Texture",
                antiAliasing = 4,
            };
            _camera.targetTexture = _texture;
            _image.texture = _texture;
            FrameCamera(width, height);
        }

        public void Release()
        {
            if (_texture == null) return;

            if (_camera != null) _camera.targetTexture = null;
            if (_image != null) _image.texture = null;
            _texture.Release();
            Object.Destroy(_texture);
            _texture = null;
        }

        static RawImage CreateRegionImage(Transform canvas, float leftFraction, float bottomFraction)
        {
            var go = new GameObject("Plane Preview", typeof(RawImage));
            go.transform.SetParent(canvas, false);

            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(leftFraction, bottomFraction);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(-MenuTheme.PadRight, 0f);
            return image;
        }

        static Camera CreateCamera(Transform owner, PlanePreviewFraming framing)
        {
            var go = new GameObject("Plane Camera", typeof(Camera));
            go.transform.SetParent(owner, false);

            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MenuTheme.Colors.Bg;
            cam.fieldOfView = framing.fieldOfView;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 2000f;

            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            return cam;
        }

        void FrameCamera(int width, int height)
        {
            float aspect = (float)width / height;
            _camera.aspect = aspect;

            float wideEnough = _planeSize / _framing.fillWidth / aspect;
            float tallEnough = _planeSize / _framing.fillHeight;
            float framedHeight = Mathf.Max(tallEnough, wideEnough);
            VisibleHeight = framedHeight / (1f - _framing.verticalMarginFraction);

            float distance = VisibleHeight * 0.5f / Mathf.Tan(_framing.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var rotation = Quaternion.Euler(_framing.viewPitchDeg, _framing.viewYawDeg, 0f);
            _camera.transform.localRotation = rotation;
            _camera.transform.localPosition = -(rotation * Vector3.forward) * distance;
        }
    }
}
