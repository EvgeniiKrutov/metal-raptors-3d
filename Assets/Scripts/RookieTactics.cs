using UnityEngine;

namespace MetalRaptors
{
    public class RookieTactics
    {
        public const float StrikeAbove = 80f;
        public const float StrikeReach = 1.4f;
        public const float Perch = 150f;
        public const float PerchSeconds = 1.5f;

        const float ZoomPitchDeg = 70f;
        const float ZoomMaxSec = 1.2f;
        const float ZoomSpeedFraction = 0.8f;
        const float PanicMinSec = 1f;
        const float PanicMaxSec = 1.5f;
        const float PanicCooldownSec = 8f;

        public bool Zooming { get; private set; }

        public bool Looping { get; private set; }

        public bool LoopRunning { get; private set; }

        public bool Panicking { get; private set; }

        public bool CanPanic => !Panicking && _panicCooldown <= 0f;

        float _t;
        float _forward;
        float _panicCooldown;

        public static float PanicSeconds => Random.Range(PanicMinSec, PanicMaxSec);

        public void Tick(float dt)
        {
            _panicCooldown = Mathf.Max(0f, _panicCooldown - dt);
        }

        public void BeginZoom(float heading)
        {
            Zooming = true;
            Looping = false;
            LoopRunning = false;
            _t = 0f;
            _forward = Mathf.Cos(heading) >= 0f ? 1f : -1f;
        }

        public float Step(float dt, float energySpeed, float flySpeed)
        {
            if (!Zooming) return float.NaN;

            _t += dt;
            if (_t >= ZoomMaxSec || energySpeed < flySpeed * ZoomSpeedFraction)
            {
                Zooming = false;
                Looping = true;
                return float.NaN;
            }

            float pitch = ZoomPitchDeg * Mathf.Deg2Rad;
            return _forward >= 0f ? pitch : Mathf.PI - pitch;
        }

        public static float LoopSweep(float heading)
        {
            float pitch = Mathf.Asin(Mathf.Clamp(Mathf.Sin(heading), -1f, 1f));
            return Mathf.Clamp(Mathf.PI - pitch, Mathf.PI * 0.5f, Mathf.PI);
        }

        public void StartLoop() => LoopRunning = true;

        public void EndLoop()
        {
            Looping = false;
            LoopRunning = false;
        }

        public void BeginPanic()
        {
            Zooming = false;
            Looping = false;
            LoopRunning = false;
            Panicking = true;
        }

        public void EndPanic()
        {
            if (!Panicking) return;
            Panicking = false;
            _panicCooldown = PanicCooldownSec;
        }

        public void Cancel()
        {
            Zooming = false;
            Looping = false;
            LoopRunning = false;
            EndPanic();
        }

        public void Reset()
        {
            Zooming = false;
            Looping = false;
            LoopRunning = false;
            Panicking = false;
            _panicCooldown = 0f;
        }
    }
}
