using UnityEngine;

namespace MetalRaptors
{
    public class PlaneModelConfig
    {
        public string resourceName;

        public string displayName;

        public string country;

        public PlaneType type;

        public string description;

        public Vector3 standUpEuler;

        public bool rollWheelsDown;

        public float onScreenSize;

        public string propPivotNode;

        public string propBladesNode;

        public PlaneStats stats;
    }

    public static class PlaneModels
    {
        const string Lorem =
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor " +
            "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud " +
            "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";

        const string LoremAlt =
            "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque " +
            "laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi " +
            "architecto beatae vitae dicta sunt explicabo, nemo enim ipsam voluptatem.";

        public static readonly PlaneModelConfig Sopwith = new PlaneModelConfig
        {
            resourceName   = "sopwith_camel",
            displayName    = "Sopwith Camel",
            country        = "Great Britain",
            type           = PlaneTypes.Fighter,
            description    = Lorem,
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            onScreenSize   = 60f,
            propPivotNode  = "propPivot",
            propBladesNode = "propBlades",
            stats = new PlaneStats
            {
                maxSpeed      = 192f,
                rotationSpeed = 180f,
                mass          = 2.5f,
                fireRate      = 5f,
                damage        = 10f,
                health        = 100f,
            },
        };

        public static readonly PlaneModelConfig Fokker = new PlaneModelConfig
        {
            resourceName   = "fokker_dr1",
            displayName    = "Fokker Dr.I",
            country        = "Germany",
            type           = PlaneTypes.Fighter,
            description    = LoremAlt,
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            onScreenSize   = 60f,
            propPivotNode  = "propPivot",
            propBladesNode = "propBlades",
            stats = new PlaneStats
            {
                maxSpeed      = 192f,
                rotationSpeed = 180f,
                mass          = 2.5f,
                fireRate      = 5f,
                damage        = 10f,
                health        = 100f,
            },
        };

        public static readonly PlaneModelConfig[] All = { Sopwith, Fokker };
    }
}
