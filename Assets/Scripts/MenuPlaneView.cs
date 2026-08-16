using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class MenuPlaneView : MonoBehaviour
    {
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

        const float VerticalMarginFraction = 2f * (RiseFraction + BobFraction) + 0.06f;

        PlanePreviewRig _rig;
        GameObject _body;

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
            if (_rig != null && _rig.Region != null) _rig.Region.SetActive(active);
            if (_body != null) _body.SetActive(active);
            gameObject.SetActive(active);
        }

        void Compose(Transform canvas, PlaneModelConfig plane)
        {
            _rig = new PlanePreviewRig(canvas, transform, new PlanePreviewFraming
            {
                fieldOfView = FieldOfView,
                viewYawDeg = ViewYawDeg,
                viewPitchDeg = ViewPitchDeg,
                fillWidth = FillWidth,
                fillHeight = FillHeight,
                verticalMarginFraction = VerticalMarginFraction,
                regionLeftFraction = MenuTheme.ColumnFraction,
                regionBottomFraction = 0f,
            }, plane.onScreenSize);

            _body = new GameObject("Menu Plane");
            _body.transform.position = PlanePreviewRig.Origin;
            PlaneFactory.BuildPlaneModel(_body.transform, plane, skin: GameManager.CurrentSkin);
        }

        void Update()
        {
            _rig.Update();
            UpdateFlight();
        }

        void UpdateFlight()
        {
            if (_body == null) return;

            Vector2 mouse = ReadMouse();
            _rise = Mathf.SmoothDamp(_rise, mouse.y * RiseFraction * _rig.VisibleHeight,
                ref _riseVelocity, MoveSmoothing);
            _roll = Mathf.SmoothDamp(_roll, mouse.x * MaxRollDeg, ref _rollVelocity, TurnSmoothing);
            _yaw = Mathf.SmoothDamp(_yaw, mouse.x * MaxYawDeg, ref _yawVelocity, TurnSmoothing);
            _pitch = Mathf.SmoothDamp(_pitch, mouse.y * MaxPitchDeg, ref _pitchVelocity, TurnSmoothing);

            float t = Time.time;
            float bob = (Mathf.Sin(t * BobSpeed) + 0.4f * Mathf.Sin(t * BobSpeed * 1.9f))
                        * BobFraction * _rig.VisibleHeight;
            float sway = Mathf.Sin(t * SwaySpeed) * SwayDeg;
            float nod = Mathf.Sin(t * SwaySpeed * 1.6f) * SwayDeg * 0.4f;

            _body.transform.position = PlanePreviewRig.Origin + Vector3.up * (_rise + bob);
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

        void OnDestroy()
        {
            if (_rig != null) _rig.Release();
            if (_body != null) Destroy(_body);
        }
    }
}
