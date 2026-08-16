using System;
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

        public static readonly PlaneModelConfig Sopwith = new PlaneModelConfig
        {
            resourceName   = "sopwith_camel",
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

        public static readonly PlaneModelConfig[] All = { Sopwith, Fokker };

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
