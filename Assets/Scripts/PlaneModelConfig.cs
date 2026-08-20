using System;
using UnityEngine;

namespace MetalRaptors
{
    public class PlaneModelConfig
    {
        public const string WorldWar1 = "objects/planes/world_war_1";

        public string resourceName;

        public string folder = WorldWar1;

        public string ResourcePath => string.IsNullOrEmpty(folder)
            ? resourceName
            : $"{folder}/{resourceName}";

        public string displayName;

        public string country;

        public PlaneType type;

        public string description;

        public Vector3 standUpEuler;

        public bool rollWheelsDown;

        public float pitchTrimDeg;

        public float onScreenSize;

        public float garageZoom = 1f;

        public string propPivotNode;

        public string propBladesNode;

        public PlaneSkin[] skins;

        public PlaneStats stats;
    }

    public static class PlaneModels
    {
        const string SopwithStory =
            "Britain's most successful scout of the war, credited with more enemy aircraft downed " +
            "than any other Allied fighter. The hump over its twin Vickers guns gave it the name, " +
            "and the torque of its rotary engine gave it a vicious right-hand turn — lethal to " +
            "novices and to the enemy alike.";

        const string FokkerStory =
            "Germany's answer to the Sopwith Triplane: three stubby wings, a light frame and a rate " +
            "of climb nothing could follow. Slow in level flight and grounded early by wing " +
            "failures, barely 320 were built — but Manfred von Richthofen flew his last in one.";

        const string AlbatrosStory =
            "The workhorse of Germany's Jastas through 1917, built around a 160 hp Mercedes and a " +
            "pair of Spandaus. Its narrow single-spar lower wing — copied from the Nieuport — bought " +
            "the pilot a clear view downward and a nasty habit of shedding itself in a hard dive. " +
            "Richthofen flew one through Bloody April, when the Jastas took four British machines " +
            "for every one they lost.";

        public static readonly PlaneModelConfig Sopwith = new PlaneModelConfig
        {
            resourceName   = "sopwith_camel",
            folder         = PlaneModelConfig.WorldWar1,
            displayName    = "Sopwith Camel",
            country        = "Great Britain",
            type           = PlaneTypes.Fighter,
            description    = SopwithStory,
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            onScreenSize   = 60f,
            propPivotNode  = "propPivot",
            propBladesNode = "propBlades",
            skins          = PlaneSkins.SopwithCamel,
            stats = new PlaneStats
            {
                maxSpeed      = 288f,
                rotationSpeed = 120f,
                mass          = 2.5f,
                fireRate      = 5f,
                damage        = 10f,
                health        = 150f,
            },
        };

        public static readonly PlaneModelConfig Fokker = new PlaneModelConfig
        {
            resourceName   = "fokker_dr1",
            folder         = PlaneModelConfig.WorldWar1,
            displayName    = "Fokker Dr.I",
            country        = "Germany",
            type           = PlaneTypes.Fighter,
            description    = FokkerStory,
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            onScreenSize   = 60f,
            propPivotNode  = "propPivot",
            propBladesNode = "propBlades",
            stats = new PlaneStats
            {
                maxSpeed      = 264f,
                rotationSpeed = 140f,
                mass          = 2.1f,
                fireRate      = 5.5f,
                damage        = 10f,
                health        = 128f,
            },
        };

        public static readonly PlaneModelConfig Albatros = new PlaneModelConfig
        {
            resourceName   = "albatros_d3",
            folder         = PlaneModelConfig.WorldWar1,
            displayName    = "Albatros D.III",
            country        = "Germany",
            type           = PlaneTypes.Fighter,
            description    = AlbatrosStory,
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            pitchTrimDeg   = 9.4f,
            onScreenSize   = 60f,
            garageZoom     = 1.1f,
            propPivotNode  = "propAssembly",
            propBladesNode = "prop",
            skins          = PlaneSkins.AlbatrosD3,
            stats = new PlaneStats
            {
                maxSpeed      = 300f,
                rotationSpeed = 104f,
                mass          = 3f,
                fireRate      = 5.5f,
                damage        = 10f,
                health        = 165f,
            },
        };

        public static readonly PlaneModelConfig[] All = { Sopwith, Fokker, Albatros };

        public static PlaneModelConfig ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (PlaneModelConfig plane in All)
            {
                if (string.Equals(plane.resourceName, id, StringComparison.OrdinalIgnoreCase))
                    return plane;

                int cut = plane.resourceName.IndexOf('_');
                if (cut > 0 && string.Equals(plane.resourceName.Substring(0, cut), id,
                        StringComparison.OrdinalIgnoreCase))
                    return plane;
            }
            return null;
        }
    }
}
