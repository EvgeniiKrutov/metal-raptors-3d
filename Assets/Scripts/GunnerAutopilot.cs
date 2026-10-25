using UnityEngine;

namespace MetalRaptors
{
    public class GunnerAutopilot : MonoBehaviour
    {
        const float Lookahead = 260f;
        const float CruiseClimbDeg = 8f;
        const float SeekClimbDeg = 14f;

        const float BandLow = 0.3f;
        const float BandHigh = 0.32f;
        const float SeekLow = 0.35f;
        const float SeekHigh = 0.7f;
        const float SeekLift = 0.25f;
        const float SeekPassed = 40f;
        const float MinSpeed = 1f;

        const float LegMinSec = 5f;
        const float LegMaxSec = 10f;
        const float CruiseResponse = 0.8f;
        const float SeekResponse = 1.6f;

        const float NoseWanderDeg = 0.6f;
        const float NoseWanderHz = 0.13f;

        const float TurbulenceLift = 10f;
        const float TurbulencePitchDeg = 1.2f;
        const float TurbulenceRollDeg = 2.5f;
        const float TurbulenceHz = 0.35f;

        const float GustGapMin = 3.5f;
        const float GustGapMax = 7f;
        const float GustSecMin = 0.6f;
        const float GustSecMax = 1.2f;
        const float GustLiftMin = 30f;
        const float GustLiftMax = 60f;
        const float GustDrag = 12f;
        const float GustRollMinDeg = 5f;
        const float GustRollMaxDeg = 9f;
        const float GustPitchPerLift = 0.05f;
        const float GustReturnBias = 0.3f;
        const float SeekGustScale = 0.4f;

        CubeController _plane;
        Rigidbody _rb;
        SupplyDrop _supply;

        float _centreY;
        float _halfHeight;
        float _low;
        float _high;
        float _legY;
        float _goalY;
        float _legLeft;
        bool _steering = true;
        float _seed;

        float _gustGap;
        float _gustAge;
        float _gustSec;
        float _gustLift;
        float _gustDrag;
        float _gustRoll;

        public static GunnerAutopilot Begin(CubeController plane, float centreY, float halfHeight)
        {
            if (plane == null) return null;

            var pilot = plane.gameObject.AddComponent<GunnerAutopilot>();
            pilot._plane = plane;
            pilot._rb = plane.GetComponent<Rigidbody>();
            pilot._centreY = centreY;
            pilot._halfHeight = halfHeight;
            pilot._low = centreY - halfHeight * BandLow;
            pilot._high = centreY + halfHeight * BandHigh;
            pilot._legY = centreY;
            pilot._goalY = plane.transform.position.y;
            pilot._seed = Random.Range(0f, 100f);
            pilot._gustGap = Random.Range(GustGapMin, GustGapMax);

            plane.EnableHeadingSteering();
            return pilot;
        }

        public void Track(SupplyDrop supply) => _supply = supply;

        public void Release() => _steering = false;

        void FixedUpdate()
        {
            if (_plane == null) return;

            float dt = Time.fixedDeltaTime;
            bool seeking = Steer(dt);
            Blow(dt, seeking);
        }

        bool Steer(float dt)
        {
            if (!_steering || !_plane.Steerable) return false;

            Vector3 pos = _plane.transform.position;
            bool seeking = Seek(pos, out float seekY);

            if (!seeking)
            {
                _legLeft -= dt;
                if (_legLeft <= 0f) NextLeg();
            }

            float goal = seeking ? seekY : _legY;
            float response = seeking ? SeekResponse : CruiseResponse;
            _goalY = Mathf.Lerp(_goalY, goal, 1f - Mathf.Exp(-response * dt));

            float climb = (seeking ? SeekClimbDeg : CruiseClimbDeg) * Mathf.Deg2Rad;
            float heading = Mathf.Clamp(Mathf.Atan2(_goalY - pos.y, Lookahead), -climb, climb);
            heading += Noise(Time.time * NoseWanderHz, 21.7f) * NoseWanderDeg * Mathf.Deg2Rad;

            _plane.SetTargetHeading(heading);
            return seeking;
        }

        bool Seek(Vector3 pos, out float y)
        {
            y = pos.y;
            if (_supply == null) return false;

            SupplyCrate crate = _supply.Crate;
            if (crate != null)
            {
                Vector3 box = crate.Box;
                float ahead = box.x - pos.x;
                if (ahead < -SeekPassed) return false;

                float speed = Mathf.Max(MinSpeed, _rb != null ? _rb.linearVelocity.x : MinSpeed);
                float reach = Mathf.Max(0f, ahead) / speed;
                y = Mathf.Clamp(box.y - SupplyCrate.FallSpeed * reach,
                    _centreY - _halfHeight * SeekLow, _centreY + _halfHeight * SeekHigh);
                return true;
            }

            if (!_supply.Incoming) return false;

            y = _centreY + _halfHeight * SeekLift;
            return true;
        }

        void NextLeg()
        {
            _legLeft = Random.Range(LegMinSec, LegMaxSec);
            _legY = Random.Range(_low, _high);
        }

        void Blow(float dt, bool seeking)
        {
            float t = Time.time * TurbulenceHz;
            float lift = Noise(t, 0f) * TurbulenceLift;
            float pitch = Noise(t, 7.3f) * TurbulencePitchDeg;
            float roll = Noise(t, 13.1f) * TurbulenceRollDeg;
            float drag = 0f;

            _gustGap -= dt;
            if (_gustGap <= 0f) StartGust();

            if (_gustAge < _gustSec)
            {
                _gustAge += dt;
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(_gustAge / _gustSec))
                          * (seeking ? SeekGustScale : 1f);

                lift += _gustLift * k;
                drag += _gustDrag * k;
                roll += _gustRoll * k;
                pitch += _gustLift * GustPitchPerLift * k;
            }

            _plane.SetWind(new Vector2(drag, lift), pitch, roll);
        }

        void StartGust()
        {
            _gustAge = 0f;
            _gustSec = Random.Range(GustSecMin, GustSecMax);
            _gustGap = _gustSec + Random.Range(GustGapMin, GustGapMax);

            float off = (_plane.transform.position.y - _centreY) / Mathf.Max(1f, _high - _centreY);
            float up = 0.5f - GustReturnBias * Mathf.Clamp(off, -1f, 1f);

            _gustLift = (Random.value < up ? 1f : -1f) * Random.Range(GustLiftMin, GustLiftMax);
            _gustDrag = Random.Range(-GustDrag, GustDrag);
            _gustRoll = (Random.value < 0.5f ? -1f : 1f) * Random.Range(GustRollMinDeg, GustRollMaxDeg);
        }

        float Noise(float t, float lane) => Mathf.PerlinNoise(_seed + lane, t) * 2f - 1f;
    }
}
