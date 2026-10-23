using UnityEngine;

namespace MetalRaptors
{
    public struct BoatSway
    {
        public float amp, rate, phase;

        public static BoatSway Roll(float ampMin, float ampMax, float secMin, float secMax) =>
            new BoatSway
            {
                amp = Random.Range(ampMin, ampMax),
                rate = 2f * Mathf.PI / Random.Range(secMin, secMax),
                phase = Random.Range(0f, 2f * Mathf.PI),
            };

        public float At(float t) => amp * Mathf.Sin(t * rate + phase);
    }
}
