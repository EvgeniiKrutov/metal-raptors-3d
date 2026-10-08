using UnityEngine;

namespace MetalRaptors
{
    public class BunkerGun : MonoBehaviour
    {
        const float IntervalMin = 6f, IntervalMax = 12f;
        const float FirstShotMin = 1f;
        const float RetryMin = 0.4f, RetryMax = 2f;
        const float ScreenInset = 0.04f;
        const float FlashMeters = 5f;
        const float SmokeMeters = 4f;

        Vector3 _muzzle;
        float _wait;
        Camera _cam;

        public void Aim(Vector3 localMuzzle)
        {
            _muzzle = localMuzzle;
            _wait = Random.Range(FirstShotMin, IntervalMax);
        }

        void Update()
        {
            _wait -= Time.deltaTime;
            if (_wait > 0f) return;

            Vector3 muzzle = transform.TransformPoint(_muzzle);
            if (!Seen(muzzle))
            {
                _wait = Random.Range(RetryMin, RetryMax);
                return;
            }

            _wait = Random.Range(IntervalMin, IntervalMax);
            float perMeter = transform.lossyScale.x;
            MuzzleFlash.Spawn(muzzle, transform.forward, FlashMeters * perMeter);
            MuzzleSmoke.Spawn(muzzle, transform.forward, SmokeMeters * perMeter);
        }

        bool Seen(Vector3 point)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return false;

            Vector3 vp = _cam.WorldToViewportPoint(point);
            return vp.z > 0f
                   && vp.x > ScreenInset && vp.x < 1f - ScreenInset
                   && vp.y > ScreenInset && vp.y < 1f - ScreenInset;
        }
    }
}
