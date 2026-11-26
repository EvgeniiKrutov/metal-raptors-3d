namespace MetalRaptors
{
    public enum PilotLevel { Rookie, Regular, Veteran }

    public readonly struct PilotSkill
    {
        public readonly float reactionSec;
        public readonly int burstMin, burstMax;
        public readonly float burstGapMin, burstGapMax;
        public readonly float trueChance, leadScale, fireRangeScale, dodgeScale;
        public readonly bool bursts;
        public readonly float missMinDeg, missMaxDeg;
        public readonly float layDeg;

        public PilotSkill(float reactionSec, int burstMin, int burstMax, float burstGapMin,
            float burstGapMax, float trueChance, float leadScale, float fireRangeScale,
            float dodgeScale, bool bursts, float missMinDeg, float missMaxDeg, float layDeg)
        {
            this.reactionSec = reactionSec;
            this.burstMin = burstMin;
            this.burstMax = burstMax;
            this.burstGapMin = burstGapMin;
            this.burstGapMax = burstGapMax;
            this.trueChance = trueChance;
            this.leadScale = leadScale;
            this.fireRangeScale = fireRangeScale;
            this.dodgeScale = dodgeScale;
            this.bursts = bursts;
            this.missMinDeg = missMinDeg;
            this.missMaxDeg = missMaxDeg;
            this.layDeg = layDeg;
        }
    }

    public static class PilotSkills
    {
        static readonly PilotSkill Rookie =
            new PilotSkill(0.9f, 6, 9, 1f, 1.8f, 0.5f, 0.75f, 1.15f, 1f, true, 3f, 5f, 0f);

        static readonly PilotSkill Regular =
            new PilotSkill(0.5f, 3, 5, 1.5f, 2.5f, 0.4f, 1f, 1f, 1f, false, 6f, 10f, 5f);

        static readonly PilotSkill Veteran =
            new PilotSkill(0.2f, 2, 3, 0.5f, 1f, 0.65f, 1f, 0.7f, 1f, true, 6f, 10f, 0f);

        public static readonly PilotSkill Elite =
            new PilotSkill(0.2f, 4, 5, 0.5f, 1f, 1f, 1f, 0.7f, 1f, true, 0f, 0f, 0f);

        public static PilotSkill For(PilotLevel level)
        {
            switch (level)
            {
                case PilotLevel.Rookie: return Rookie;
                case PilotLevel.Veteran: return Veteran;
                default: return Regular;
            }
        }
    }
}
