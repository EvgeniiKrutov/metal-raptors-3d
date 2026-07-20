using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// A short, decaying position-and-roll wobble on whatever transform it sits on — used on a
    /// plane model to sell a scrape without moving the physics body (the body flies straight on;
    /// only the visible model shivers). <see cref="Play"/> restarts the shake; it eases back to
    /// the captured rest pose and then holds still until the next hit.
    ///
    /// The rest pose is captured lazily on the first <see cref="Play"/>, so the model's built
    /// orientation and offset (set by <see cref="LevelController"/> before this ever fires) are
    /// treated as zero — the wobble is always applied on top of them, never instead of them.
    /// </summary>
    public class ShakeEffect : MonoBehaviour
    {
        // Kept small on purpose: the model carries the plane's collider, so a big translational
        // jolt could brush the ground during a low scrape. A couple of metres reads clearly at the
        // camera's ~420 m distance without throwing the hitbox around.
        const float Magnitude = 2.2f;       // metres of positional jitter at full strength
        const float AngleMagnitude = 8f;    // degrees of roll (about the view axis) at full strength
        const float Duration = 0.35f;       // seconds to decay from full strength to rest

        Vector3 _restPos;
        Quaternion _restRot;
        bool _captured;
        bool _shaking;
        float _timeLeft;

        /// <summary>Kick off (or restart) the wobble at full strength.</summary>
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
            if (!_shaking) return; // idle: no per-frame work until the next Play

            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f)
            {
                transform.localPosition = _restPos;
                transform.localRotation = _restRot;
                _shaking = false;
                return;
            }

            float k = _timeLeft / Duration; // 1 -> 0 over the shake, so it eases out

            Vector2 off = Random.insideUnitCircle * (Magnitude * k);
            transform.localPosition = _restPos + new Vector3(off.x, off.y, 0f);

            float roll = Random.Range(-1f, 1f) * AngleMagnitude * k;
            transform.localRotation = _restRot * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
