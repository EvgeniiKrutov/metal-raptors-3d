using System;
using UnityEngine;

namespace MetalRaptors
{
    public class PlaneModelConfig
    {
        public const string WorldWar1 = "objects/planes/world_war_1";

        public const float UnitsPerMeter = 66f / 8.5f;

        public const float LevelScale = 1.2f;

        public string resourceName;

        public string scriptId;

        public string folder = WorldWar1;

        public string ResourcePath => string.IsNullOrEmpty(folder)
            ? resourceName
            : $"{folder}/{resourceName}";

        public string displayName;

        public string country;

        public PlaneType type;

        public EnemyRole enemyRole = EnemyRole.Fighter;

        public string description;

        public Vector3 standUpEuler;

        public bool rollWheelsDown;

        public float pitchTrimDeg;

        public float lengthMeters;

        public float wingspanMeters;

        public float heightMeters;

        public bool fitWingspan;

        public float pivotFromNose;

        public float pivotHeight;

        public float LengthUnits => lengthMeters * UnitsPerMeter;

        public float OnScreenSize => wingspanMeters * UnitsPerMeter;

        public float garageZoom = 1f;

        public string propPivotNode;

        public string propBladesNode;

        public bool straightenProp;

        public float gunLift;

        public float gunSetback;

        public string gunNode;

        public float bombLift;

        public string engineNode;

        public string wingNode;

        public string rearGunNode;

        public string hitboxNode;

        public float hitboxScale = 1f;

        public float hitboxFloor;

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

        const string EindeckerStory =
            "The machine behind the Fokker Scourge, unremarkable in every way but one: a gun that " +
            "fired through its own propeller arc. Wing-warping made it slow to answer the stick and " +
            "the 100 hp Oberursel gave it nothing in a climb, but for one winter over the Western " +
            "Front an aeroplane that could aim itself was enough. Immelmann and Boelcke learned " +
            "the trade in it.";

        const string AircoStory =
            "Geoffrey de Havilland's two-seat day bomber, and on a 375 hp Rolls-Royce Eagle fast " +
            "enough to outrun most of the scouts sent up to catch it. A Vickers for the pilot and a " +
            "Lewis on a ring for the gunner gave it teeth both ways — but the main fuel tank sat " +
            "between the two cockpits, and the Americans who built it by the thousand called it " +
            "the Flaming Coffin.";

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
            pitchTrimDeg   = 10f,
            lengthMeters   = 5.72f,
            wingspanMeters = 8.5f,
            heightMeters   = 2.59f,
            fitWingspan    = true,
            pivotFromNose  = 0.43f,
            propPivotNode  = "Propeller",
            propBladesNode = "prop_mesh",
            gunNode        = "guns_mesh",
            bombLift       = 0.15f,
            engineNode     = "cowling",
            wingNode       = "upper_wing",
            hitboxNode     = "fuselage_shell",
            hitboxFloor    = 0.4f,
            skins          = PlaneSkins.SopwithCamel,
            stats = new PlaneStats
            {
                maxSpeed      = 288f,
                rotationSpeed = 120f,
                mass          = 2.5f,
                fireRate      = 5f,
                damage        = 10f,
                health        = 200f,
            },
        };

        public static readonly PlaneModelConfig Fokker = new PlaneModelConfig
        {
            resourceName   = "fokker_dr1",
            folder         = PlaneModelConfig.WorldWar1,
            displayName    = "Fokker Dr.I",
            country        = "Germany",
            type           = PlaneTypes.Fighter,
            enemyRole      = EnemyRole.Scout,
            description    = FokkerStory,
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            pitchTrimDeg   = 10f,
            lengthMeters   = 5.77f,
            wingspanMeters = 7.19f,
            heightMeters   = 2.95f,
            fitWingspan    = true,
            pivotFromNose  = 0.41f,
            propPivotNode  = "Propeller",
            propBladesNode = "prop_mesh",
            gunNode        = "guns_mesh",
            gunLift        = 0.093f,
            gunSetback     = 0.121f,
            engineNode     = "cowling_shell",
            wingNode       = "upper_wing",
            hitboxNode     = "fuselage_shell",
            hitboxFloor    = 0.34f,
            skins          = PlaneSkins.FokkerDr1,
            stats = new PlaneStats
            {
                maxSpeed      = 264f,
                rotationSpeed = 140f,
                mass          = 2.1f,
                fireRate      = 5.5f,
                damage        = 10f,
                health        = 171f,
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
            lengthMeters   = 7.35f,
            wingspanMeters = 9f,
            heightMeters   = 2.8f,
            garageZoom     = 1.1f,
            propPivotNode  = "propAssembly",
            propBladesNode = "prop",
            gunLift        = 0.080f,
            gunSetback     = 0.142f,
            skins          = PlaneSkins.AlbatrosD3,
            stats = new PlaneStats
            {
                maxSpeed      = 300f,
                rotationSpeed = 104f,
                mass          = 3f,
                fireRate      = 5.5f,
                damage        = 10f,
                health        = 220f,
            },
        };

        public static readonly PlaneModelConfig Eindecker = new PlaneModelConfig
        {
            resourceName   = "fokker_eindecker",
            scriptId       = "eindecker",
            folder         = PlaneModelConfig.WorldWar1,
            displayName    = "Fokker E.III",
            country        = "Germany",
            type           = PlaneTypes.Scout,
            enemyRole      = EnemyRole.Scout,
            description    = EindeckerStory,
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            pitchTrimDeg   = 10f,
            lengthMeters   = 7.2f,
            wingspanMeters = 9.54f,
            heightMeters   = 2.8f,
            fitWingspan    = true,
            pivotFromNose  = 0.51f,
            pivotHeight    = 0.58f,
            propPivotNode  = "Propeller",
            propBladesNode = "prop_mesh",
            gunNode        = "gun_mesh",
            gunLift        = 0.083f,
            gunSetback     = 0.038f,
            engineNode     = "cowling_shell",
            wingNode       = "Wings",
            hitboxNode     = "fuselage_box",
            hitboxFloor    = 0.42f,
            skins          = PlaneSkins.FokkerEindecker,
            stats = new PlaneStats
            {
                maxSpeed      = 210f,
                rotationSpeed = 95f,
                mass          = 1.8f,
                fireRate      = 4.5f,
                damage        = 9f,
                health        = 140f,
            },
        };

        public static readonly PlaneModelConfig AircoDh4 = new PlaneModelConfig
        {
            resourceName   = "airco_dh4",
            folder         = PlaneModelConfig.WorldWar1,
            displayName    = "Airco DH.4",
            country        = "Great Britain",
            type           = PlaneTypes.Bomber,
            description    = AircoStory,
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            pitchTrimDeg   = 10f,
            lengthMeters   = 9.35f,
            wingspanMeters = 12.92f,
            heightMeters   = 3.35f,
            propPivotNode  = "propeller",
            propBladesNode = "prop_blade_mesh",
            gunNode        = "front_guns_mesh",
            rearGunNode    = "back_guns_mesh",
            hitboxNode     = "fuselage",
            hitboxScale    = 1.5f,
            skins          = PlaneSkins.AircoDh4,
            stats = new PlaneStats
            {
                maxSpeed      = 300f,
                rotationSpeed = 90f,
                mass          = 3.4f,
                fireRate      = 6.5f,
                damage        = 10f,
                health        = 260f,
            },
        };

        public static readonly PlaneModelConfig[] All =
            { Sopwith, Fokker, Albatros, Eindecker, AircoDh4 };

        public static PlaneModelConfig EnemyFor(EnemyRole role) =>
            role == EnemyRole.Scout ? Eindecker : Albatros;

        public static PlaneModelConfig ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (PlaneModelConfig plane in All)
            {
                if (string.Equals(plane.resourceName, id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(plane.scriptId, id, StringComparison.OrdinalIgnoreCase))
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
