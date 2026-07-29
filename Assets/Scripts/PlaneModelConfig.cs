using UnityEngine;

namespace MetalRaptors
{
    public class PlaneModelConfig
    {
        public string resourceName;

        public Vector3 standUpEuler;

        public bool rollWheelsDown;

        public float onScreenSize;

        public string propPivotNode;

        public string propBladesNode;
    }

    public static class PlaneModels
    {
        public static readonly PlaneModelConfig Fokker = new PlaneModelConfig
        {
            resourceName   = "fokker_dr1",
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            onScreenSize   = 60f,
            propPivotNode  = "propPivot",
            propBladesNode = "propBlades",
        };

        public static readonly PlaneModelConfig Sopwith = new PlaneModelConfig
        {
            resourceName   = "sopwith_camel",
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            onScreenSize   = 60f,
            propPivotNode  = "propPivot",
            propBladesNode = "propBlades",
        };
    }
}
