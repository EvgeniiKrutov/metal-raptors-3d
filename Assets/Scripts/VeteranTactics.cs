using UnityEngine;

namespace MetalRaptors
{
    public enum VeteranPhase { Setup, Run, Zoom, Wingover, Reverse, Feint, DiveOut, Break }

    public enum VeteranEntry { AboveBehind, Overhead, AboveFront }

    public class VeteranTactics
    {
        public const float SetupLift = 230f;
        public const float OverheadLift = 270f;
        public const float SetupFront = 0.35f;
        public const float SetupBehind = 0.35f;
        public const float SetupReach = 120f;
        public const float SetupMaxSec = 3.5f;
        public const float RunGraceSec = 0.7f;
        public const float RunAbortDeg = 60f;
        public const float RunCloseRange = 80f;
        public const float RunMaxSec = 3f;
        public const float RunOpenDeg = 20f;
        public const float ZoomSpeedFraction = 0.85f;
        public const float OnSixSec = 0.5f;
        public const float TailConeDeg = 60f;
        public const float NoseOnMeDeg = 30f;
        public const float FeintRange = 600f;
        public const float HeadOnDot = -0.7f;
        public const float HeadOnConeDeg = 12f;
        public const float HeadOnRangeFactor = 1.2f;
        public const float DiveOutSpeed = 1.3f;
        public const float ReverseClimbRoom = 160f;
        public const float ReverseDiveRoom = 260f;
        public const float ReverseEnergy = 0.9f;

        const float ZoomPitchDeg = 60f;
        const float ZoomMaxSec = 1.3f;
        const float FeintOutSec = 0.35f;
        const float FeintBackSec = 0.8f;
        const float FeintAngleDeg = 90f;
        const float DiveOutPitchDeg = 50f;
        const float DiveOutMaxSec = 3f;
        const float BreakPitchDeg = 55f;
        const float BreakMinSec = 0.35f;
        const float BreakMaxSec = 1.2f;
        const float QuickShare = 0.5f;

        public VeteranPhase Phase { get; private set; }

        public VeteranEntry Entry { get; private set; }

        public bool Armed => Phase == VeteranPhase.Run;

        public bool Looping => Phase == VeteranPhase.Wingover || Phase == VeteranPhase.Reverse;

        public float Elapsed => _t;

        public bool Quick { get; set; }

        public float SetupLimit => Quick ? SetupMaxSec * QuickShare : SetupMaxSec;

        float ZoomLimit => Quick ? ZoomMaxSec * QuickShare : ZoomMaxSec;

        float _t;
        float _from;
        float _side;
        float _forward = 1f;

        public void Reset()
        {
            PickEntry();
            Enter(VeteranPhase.Setup);
        }

        public void PickEntry()
        {
            var next = (VeteranEntry)Random.Range(0, 3);
            if (next == Entry) next = (VeteranEntry)(((int)next + 1) % 3);
            Entry = next;
        }

        public void BeginSetup() => Enter(VeteranPhase.Setup);

        public void BeginRun() => Enter(VeteranPhase.Run);

        public void BeginZoom(float heading)
        {
            Enter(VeteranPhase.Zoom);
            _forward = Mathf.Cos(heading) >= 0f ? 1f : -1f;
        }

        public void BeginWingover() => Enter(VeteranPhase.Wingover);

        public void BeginReverse() => Enter(VeteranPhase.Reverse);

        public void BeginFeint(float heading, float side)
        {
            Enter(VeteranPhase.Feint);
            _from = heading;
            _side = side;
        }

        public void BeginDiveOut(float heading)
        {
            Enter(VeteranPhase.DiveOut);
            _forward = Mathf.Cos(heading) >= 0f ? 1f : -1f;
        }

        public void BeginBreak(float heading, float side)
        {
            Enter(VeteranPhase.Break);
            _forward = Mathf.Cos(heading) >= 0f ? 1f : -1f;
            _side = side;
        }

        void Enter(VeteranPhase phase)
        {
            Phase = phase;
            _t = 0f;
        }

        public void Tick(float dt) => _t += dt;

        public bool ZoomDone(float energySpeed, float flySpeed) =>
            Phase == VeteranPhase.Zoom
            && (_t >= ZoomLimit || energySpeed < flySpeed * ZoomSpeedFraction);

        public bool FeintDone => Phase == VeteranPhase.Feint && _t >= FeintOutSec + FeintBackSec;

        public bool DiveOutDone(float energySpeed, float flySpeed) =>
            Phase == VeteranPhase.DiveOut
            && (energySpeed > flySpeed * DiveOutSpeed || _t >= DiveOutMaxSec);

        public bool BreakDone(bool clear) =>
            Phase == VeteranPhase.Break && (_t >= BreakMaxSec || (clear && _t >= BreakMinSec));

        public float Heading(float heading, float setupHeading, float runHeading)
        {
            switch (Phase)
            {
                case VeteranPhase.Run:
                    return runHeading;

                case VeteranPhase.Zoom:
                    return Pitched(ZoomPitchDeg, _forward);

                case VeteranPhase.Feint:
                {
                    float swing = FeintAngleDeg * Mathf.Deg2Rad;
                    return _t < FeintOutSec ? _from + _side * swing : _from - _side * swing;
                }

                case VeteranPhase.DiveOut:
                    return Pitched(-DiveOutPitchDeg, _forward);

                case VeteranPhase.Break:
                    return Pitched(_side * BreakPitchDeg, _forward);

                case VeteranPhase.Setup:
                    return setupHeading;

                default:
                    return heading;
            }
        }

        static float Pitched(float degrees, float forward)
        {
            float pitch = degrees * Mathf.Deg2Rad;
            return forward >= 0f ? pitch : Mathf.PI - pitch;
        }
    }
}
