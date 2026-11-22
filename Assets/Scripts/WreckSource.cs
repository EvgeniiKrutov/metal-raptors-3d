using UnityEngine;

namespace MetalRaptors
{
    public class WreckSource : MonoBehaviour
    {
        public PlaneModelConfig Plane { get; private set; }

        public PlaneSkin Skin { get; private set; }

        public Transform Model { get; private set; }

        public static WreckSource Attach(GameObject body, Transform model, PlaneModelConfig plane, PlaneSkin skin)
        {
            if (body == null || plane == null) return null;

            var source = body.GetComponent<WreckSource>();
            if (source == null) source = body.AddComponent<WreckSource>();
            source.Plane = plane;
            source.Skin = skin;
            source.Model = model;
            return source;
        }
    }
}
