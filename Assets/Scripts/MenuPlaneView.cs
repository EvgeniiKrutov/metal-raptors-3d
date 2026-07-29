using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuPlaneView : MonoBehaviour
    {
        static readonly Vector3 RigOrigin = new Vector3(0f, 5000f, 0f);

        const float RegionPadRight = MenuTheme.PadRight;

        const float FieldOfView = 32f;
        const float ViewYawDeg = -20f;
        const float ViewPitchDeg = 8f;
        const float FillWidth = 1f;
        const float FillHeight = 0.95f;

        const float RiseFraction = 0.12f;
        const float MaxRollDeg = 35f;
        const float MaxYawDeg = 12f;
        const float MaxPitchDeg = 10f;
        const float MoveSmoothing = 0.35f;
        const float TurnSmoothing = 0.4f;

        const float BobFraction = 0.02f;
        const float BobSpeed = 0.9f;
        const float SwayDeg = 3f;
        const float SwaySpeed = 0.7f;

        // Extra headroom above/below the plane's own framed height so the rise + bob travel
        // (up to RiseFraction + BobFraction of the visible height, either way) never pushes
        // the model past the top or bottom of the frame.
        const float VerticalMarginFraction = 2f * (RiseFraction + BobFraction) + 0.06f;

        Canvas _canvas;
        RawImage _image;
        RenderTexture _texture;
        Camera _camera;

        GameObject _body;
        float _planeSize;
        float _visibleHeight;

        float _rise, _riseVelocity;
        float _roll, _rollVelocity;
        float _yaw, _yawVelocity;
        float _pitch, _pitchVelocity;

        public static MenuPlaneView Build(Transform canvas, PlaneModelConfig plane)
        {
            var go = new GameObject("Menu Plane View");
            var view = go.AddComponent<MenuPlaneView>();
            view.Compose(canvas, plane);
            return view;
        }

        public void SetActive(bool active)
        {
            if (_image != null) _image.gameObject.SetActive(active);
            if (_body != null) _body.SetActive(active);
            gameObject.SetActive(active);
        }

        void Compose(Transform canvas, PlaneModelConfig plane)
        {
            _canvas = canvas.GetComponentInParent<Canvas>();
            _image = CreateRegionImage(canvas);

            transform.position = RigOrigin;
            _camera = CreateCamera();

            _planeSize = plane.onScreenSize;
            _body = new GameObject("Menu Plane");
            _body.transform.position = RigOrigin;
            PlaneFactory.BuildPlaneModel(_body.transform, plane);
        }

        RawImage CreateRegionImage(Transform canvas)
        {
            var go = new GameObject("Plane Preview", typeof(RawImage));
            go.transform.SetParent(canvas, false);

            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(MenuTheme.ColumnFraction, 0f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(-RegionPadRight, 0f);
            return image;
        }

        Camera CreateCamera()
        {
            var go = new GameObject("Plane Camera", typeof(Camera));
            go.transform.SetParent(transform, false);

            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MenuTheme.Colors.Bg;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 2000f;

            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            return cam;
        }

        void Update()
        {
            UpdateTexture();
            UpdateFlight();
        }

        void UpdateTexture()
        {
            if (_canvas == null || _image == null) return;

            Rect rect = _image.rectTransform.rect;
            int width = Mathf.Max(16, Mathf.RoundToInt(rect.width * _canvas.scaleFactor));
            int height = Mathf.Max(16, Mathf.RoundToInt(rect.height * _canvas.scaleFactor));
            if (_texture != null && _texture.width == width && _texture.height == height) return;

            ReleaseTexture();
            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
            {
                name = "Menu Plane Texture",
                antiAliasing = 4,
            };
            _camera.targetTexture = _texture;
            _image.texture = _texture;
            FrameCamera(width, height);
        }

        void FrameCamera(int width, int height)
        {
            float aspect = (float)width / height;
            _camera.aspect = aspect;

            float wideEnough = _planeSize / FillWidth / aspect;
            float tallEnough = _planeSize / FillHeight;
            float framedHeight = Mathf.Max(tallEnough, wideEnough);
            _visibleHeight = framedHeight / (1f - VerticalMarginFraction);

            float distance = _visibleHeight * 0.5f / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            var rotation = Quaternion.Euler(ViewPitchDeg, ViewYawDeg, 0f);
            _camera.transform.localRotation = rotation;
            _camera.transform.localPosition = -(rotation * Vector3.forward) * distance;
        }

        void UpdateFlight()
        {
            if (_body == null) return;

            Vector2 mouse = ReadMouse();
            _rise = Mathf.SmoothDamp(_rise, mouse.y * RiseFraction * _visibleHeight,
                ref _riseVelocity, MoveSmoothing);
            _roll = Mathf.SmoothDamp(_roll, mouse.x * MaxRollDeg, ref _rollVelocity, TurnSmoothing);
            _yaw = Mathf.SmoothDamp(_yaw, mouse.x * MaxYawDeg, ref _yawVelocity, TurnSmoothing);
            _pitch = Mathf.SmoothDamp(_pitch, mouse.y * MaxPitchDeg, ref _pitchVelocity, TurnSmoothing);

            float t = Time.time;
            float bob = (Mathf.Sin(t * BobSpeed) + 0.4f * Mathf.Sin(t * BobSpeed * 1.9f))
                        * BobFraction * _visibleHeight;
            float sway = Mathf.Sin(t * SwaySpeed) * SwayDeg;
            float nod = Mathf.Sin(t * SwaySpeed * 1.6f) * SwayDeg * 0.4f;

            _body.transform.position = RigOrigin + Vector3.up * (_rise + bob);
            _body.transform.rotation = Quaternion.Euler(0f, _yaw, 0f)
                                     * Quaternion.Euler(0f, 0f, _pitch + nod)
                                     * Quaternion.Euler(_roll + sway, 0f, 0f);
        }

        static Vector2 ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;

            Vector2 p = mouse.position.ReadValue();
            return new Vector2(
                Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f),
                Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
        }

        void ReleaseTexture()
        {
            if (_texture == null) return;

            if (_camera != null) _camera.targetTexture = null;
            if (_image != null) _image.texture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        void OnDestroy()
        {
            ReleaseTexture();
            if (_body != null) Destroy(_body);
        }
    }
}
