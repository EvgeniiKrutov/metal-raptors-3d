using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class GunnerAutopilot : MonoBehaviour
    {
        const float Lookahead = 260f;
        const float SeekNear = 180f;
        const float CruiseClimbDeg = 8f;
        const float SeekClimbDeg = 18f;
        const float RecoverClimbDeg = 14f;

        const float BandLow = 0.3f;
        const float BandHigh = 0.32f;
        const float SeekLow = 0.35f;
        const float SeekHigh = 0.7f;
        const float SeekPassed = 40f;
        const float MinSpeed = 1f;

        const float LegMinSec = 5f;
        const float LegMaxSec = 10f;
        const float CruiseResponse = 0.8f;

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
        const float SeekGustScale = 0.25f;

        const float DiveDeg = 38f;
        const float DiveReach = 120f;
        const float DiveDrop = 0.75f;
        const float DiveFloor = 0.62f;
        const float DiveArrive = 20f;
        const float DiveSink = 70f;
        const float DiveSinkEase = 60f;
        const float DiveSinkRampSec = 0.2f;
        const float DivePushMaxSec = 2f;
        const float DiveHoldSec = 0.7f;
        const float PullDeg = 16f;
        const float PullSec = 1.1f;
        const float DiveCooldown = 8f;

        enum DivePhase { Off, Push, Hold, Pull }

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

        DivePhase _phase;
        float _diveAge;
        float _diveFloorY;
        float _cooldown;

        public bool Locked { get; set; }

        public bool IsRunning => _phase != DivePhase.Off;

        public bool IsReady => enabled && !Locked && _steering && _cooldown <= 0f
                               && _phase == DivePhase.Off && !CinematicBars.AnyShowing;

        public float Charge => IsRunning ? 1f : 1f - Mathf.Clamp01(_cooldown / DiveCooldown);

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

        public void Release()
        {
            _steering = false;
            if (IsRunning) EndDive();
        }

        public void Request()
        {
            if (!IsReady || GameMenu.IsOpen || LevelBriefing.IsOpen || !_plane.Steerable) return;

            float y = _plane.transform.position.y;
            _diveFloorY = Mathf.Max(y - _halfHeight * DiveDrop, _centreY - _halfHeight * DiveFloor);
            if (_diveFloorY >= y - DiveArrive) return;

            _phase = DivePhase.Push;
            _diveAge = 0f;
        }

        void Update()
        {
            if (GameMenu.IsOpen || LevelBriefing.IsOpen) return;
            if (_phase == DivePhase.Off) _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);

            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Request();
        }

        void FixedUpdate()
        {
            if (_plane == null) return;

            float dt = Time.fixedDeltaTime;
            bool seeking = Steer(dt);
            Blow(dt, seeking || IsRunning);
        }

        bool Steer(float dt)
        {
            if (!_steering || !_plane.Steerable)
            {
                if (IsRunning) EndDive();
                return false;
            }

            Vector3 pos = _plane.transform.position;
            if (IsRunning)
            {
                _plane.SetTargetHeading(DiveHeading(pos, dt));
                return false;
            }

            float heading;
            bool seeking = Seek(pos, out float ahead, out float seekY);

            if (seeking)
            {
                _goalY = pos.y;
                float climb = SeekClimbDeg * Mathf.Deg2Rad;
                float reach = Mathf.Clamp(ahead, SeekNear, Lookahead);
                heading = Mathf.Clamp(Mathf.Atan2(seekY - pos.y, reach), -climb, climb);
            }
            else
            {
                _legLeft -= dt;
                if (_legLeft <= 0f) NextLeg();

                _goalY = Mathf.Lerp(_goalY, _legY, 1f - Mathf.Exp(-CruiseResponse * dt));
                float climb = (pos.y < _low ? RecoverClimbDeg : CruiseClimbDeg) * Mathf.Deg2Rad;
                heading = Mathf.Clamp(Mathf.Atan2(_goalY - pos.y, Lookahead), -climb, climb);
                heading += Noise(Time.time * NoseWanderHz, 21.7f) * NoseWanderDeg * Mathf.Deg2Rad;
            }

            _plane.SetTargetHeading(heading);
            return seeking;
        }

        float DiveHeading(Vector3 pos, float dt)
        {
            _diveAge += dt;

            if (_phase == DivePhase.Push
                && (pos.y <= _diveFloorY + DiveArrive || _diveAge >= DivePushMaxSec))
                NextPhase(DivePhase.Hold);
            else if (_phase == DivePhase.Hold && _diveAge >= DiveHoldSec)
                NextPhase(DivePhase.Pull);
            else if (_phase == DivePhase.Pull && _diveAge >= PullSec)
                EndDive();

            if (_phase != DivePhase.Push && _phase != DivePhase.Hold)
                return PullDeg * Mathf.Deg2Rad;

            float steep = DiveDeg * Mathf.Deg2Rad;
            return Mathf.Clamp(Mathf.Atan2(_diveFloorY - pos.y, DiveReach), -steep, steep);
        }

        void NextPhase(DivePhase phase)
        {
            _phase = phase;
            _diveAge = 0f;
        }

        void EndDive()
        {
            _phase = DivePhase.Off;
            _cooldown = DiveCooldown;
            _goalY = _plane.transform.position.y;
            NextLeg();
        }

        float Sink()
        {
            if (_phase != DivePhase.Push) return 0f;

            float above = _plane.transform.position.y - _diveFloorY - DiveArrive;
            return DiveSink * Mathf.Clamp01(above / DiveSinkEase)
                   * Mathf.Clamp01(_diveAge / DiveSinkRampSec);
        }

        bool Seek(Vector3 pos, out float ahead, out float y)
        {
            ahead = 0f;
            y = pos.y;
            if (_supply == null || !_supply.Aim(out Vector3 box)) return false;

            ahead = box.x - pos.x;
            if (ahead < -SeekPassed) return false;

            float speed = Mathf.Max(MinSpeed, _rb != null ? _rb.linearVelocity.x : MinSpeed);
            float reach = Mathf.Max(0f, ahead) / speed;
            y = Mathf.Clamp(box.y - SupplyCrate.FallSpeed * reach,
                _centreY - _halfHeight * SeekLow, _centreY + _halfHeight * SeekHigh);
            return true;
        }

        void NextLeg()
        {
            _legLeft = Random.Range(LegMinSec, LegMaxSec);
            _legY = Random.Range(_low, _high);
        }

        void Blow(float dt, bool calmed)
        {
            float t = Time.time * TurbulenceHz;
            float calm = calmed ? SeekGustScale : 1f;
            float lift = Noise(t, 0f) * TurbulenceLift * calm;
            float pitch = Noise(t, 7.3f) * TurbulencePitchDeg;
            float roll = Noise(t, 13.1f) * TurbulenceRollDeg;
            float drag = 0f;

            _gustGap -= dt;
            if (_gustGap <= 0f) StartGust();

            if (_gustAge < _gustSec)
            {
                _gustAge += dt;
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(_gustAge / _gustSec)) * calm;

                lift += _gustLift * k;
                drag += _gustDrag * k;
                roll += _gustRoll * k;
                pitch += _gustLift * GustPitchPerLift * k;
            }

            _plane.SetWind(new Vector2(drag, lift - Sink()), pitch, roll);
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
