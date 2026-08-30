using UnityEngine;

namespace MetalRaptors
{
    public class ShakeEffect : MonoBehaviour
    {
        const float Magnitude = 2.2f;
        const float AngleMagnitude = 8f;
        const float Duration = 0.35f;

        Vector3 _restPos;
        Quaternion _restRot;
        bool _captured;
        bool _shaking;
        float _timeLeft;

        public void Play()
        {
            if (!_captured)
            {
                _restPos = transform.localPosition;
                _restRot = transform.localRotation;
                _captured = true;
            }
            _timeLeft = Duration;
            _shaking = true;
        }

        void LateUpdate()
        {
            if (!_shaking) return;

            _timeLeft -= Time.unscaledDeltaTime;
            if (_timeLeft <= 0f)
            {
                transform.localPosition = _restPos;
                transform.localRotation = _restRot;
                _shaking = false;
                return;
            }

            float k = _timeLeft / Duration;

            Vector2 off = Random.insideUnitCircle * (Magnitude * k);
            transform.localPosition = _restPos + new Vector3(off.x, off.y, 0f);

            float roll = Random.Range(-1f, 1f) * AngleMagnitude * k;
            transform.localRotation = _restRot * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
