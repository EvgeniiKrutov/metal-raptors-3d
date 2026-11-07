using UnityEngine;

namespace MetalRaptors
{
    public class NearBlasts
    {
        const float FirstMin = 1.5f, FirstMax = 3f;
        const float GapMin = 3f, GapMax = 5f;
        const float NearLengths = 1f, FarLengths = 2f;
        const float SizeMin = 0.45f, SizeMax = 0.65f;
        const float DepthFront = 15f, DepthBack = 40f;
        const float ShakeNear = 1f, ShakeFar = 0.65f;
        const float JoltDeg = 6f;
        const float WarnSec = 1f;

        readonly Transform _plane;
        readonly float _length;
        readonly float _floorY;
        readonly RearGun _gun;
        readonly System.Action<float> _shake;
        readonly System.Action<Vector3> _fire;

        float _timer;
        Vector3 _last;
        Vector3 _velocity;
        bool _tracked;

        public NearBlasts(Transform plane, float length, float floorY, RearGun gun,
            System.Action<float> shake, System.Action<Vector3> fire = null)
        {
            _plane = plane;
            _length = Mathf.Max(1f, length);
            _floorY = floorY;
            _gun = gun;
            _shake = shake;
            _fire = fire;
            _timer = Random.Range(FirstMin, FirstMax);
        }

        public void Tick(float dt)
        {
            if (_plane == null || dt <= 0f) return;

            Vector3 plane = _plane.position;
            if (_tracked) _velocity = (plane - _last) / dt;
            _last = plane;
            _tracked = true;

            _timer -= dt;
            if (_timer > 0f) return;
            _timer = Random.Range(GapMin, GapMax);

            float reach = Random.Range(NearLengths, FarLengths);
            float angle = Random.Range(0f, 2f * Mathf.PI);
            float dx = Mathf.Cos(angle) * reach * _length;
            float dy = Mathf.Sin(angle) * reach * _length;

            Vector3 ahead = plane + new Vector3(_velocity.x, _velocity.y, 0f) * WarnSec;
            if (ahead.y + dy < _floorY) dy = Mathf.Abs(dy);

            var at = new Vector3(ahead.x + dx, ahead.y + dy, plane.z + Random.Range(-DepthFront, DepthBack));
            float strength = Mathf.Lerp(ShakeNear, ShakeFar, Mathf.InverseLerp(NearLengths, FarLengths, reach));

            _fire?.Invoke(at);
            SkyBlast.Arm(at, _length * Random.Range(SizeMin, SizeMax), WarnSec, null, 0f,
                onBlow: () => Blow(strength));
        }

        void Blow(float strength)
        {
            _shake?.Invoke(strength);
            if (_gun != null) _gun.Jolt(JoltDeg * strength);
        }
    }
}
