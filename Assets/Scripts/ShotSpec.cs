using System;

namespace MetalRaptors
{
    [Serializable]
    public class ShotSpec
    {
        public string output;
        public int width = 1920;
        public int height = 1080;
        public int supersample = 2;
        public string terrain = "Verdun";
        public int seed = 1917;
        public string daytime = "Morning";
        public float settle = 2.5f;
        public float warmup = 3f;
        public float fogStart = -1f;
        public float fogEnd = -1f;
        public bool ambientBlasts;
        public bool ambientFlak;
        public bool ambientZeppelins;
        public bool ambientPeople = true;
        public bool ambientClouds = true;
        public bool ambientShips = true;
        public bool ambientGulls = true;
        public bool bunkerFire;
        public bool aerodrome;
        public bool aerodromeFires;
        public bool darkHangars;
        public float clearNear;
        public float clearCoast;
        public bool clearGroynes;
        public ShotCamera camera = new ShotCamera();
        public ShotFocus focus = new ShotFocus();
        public ShotPlane[] planes = new ShotPlane[0];
        public ShotSquad[] squads = new ShotSquad[0];
        public ShotProp[] props = new ShotProp[0];
        public ShotBurst[] groundBlasts = new ShotBurst[0];
        public ShotBurst[] flak = new ShotBurst[0];
        public ShotBurst[] explosions = new ShotBurst[0];
        public ShotSmoke[] smoke = new ShotSmoke[0];
        public ShotZeppelin[] zeppelins = new ShotZeppelin[0];
        public ShotCrater[] craters = new ShotCrater[0];
        public ShotCloud[] clouds = new ShotCloud[0];
        public ShotShip[] ships = new ShotShip[0];
        public ShotBunker[] bunkers = new ShotBunker[0];
        public ShotBoat[] boats = new ShotBoat[0];
        public ShotGroyne[] groynes = new ShotGroyne[0];
        public ShotGull[] gulls = new ShotGull[0];
        public ShotBurst[] splashes = new ShotBurst[0];
        public ShotMistSpec mist = new ShotMistSpec();
        public ShotFill planeFill = new ShotFill();
        public ShotArena arena = new ShotArena();
    }

    [Serializable]
    public class ShotArena
    {
        public bool on;
        public float distance;
        public float fov;
        public float[] eye;
        public int seed;
    }

    [Serializable]
    public class ShotCamera
    {
        public float x;
        public float z = 40f;
        public float height = 12f;
        public float yaw;
        public float pitch;
        public float roll;
        public float fov = 40f;
    }

    [Serializable]
    public class ShotFocus
    {
        public float distance;
        public float focalLength = 85f;
        public float aperture = 4f;
        public float farStart;
        public float farEnd;
        public float farRadius;
    }

    [Serializable]
    public class ShotPlane
    {
        public string model;
        public string skin;
        public float[] screen;
        public float distance;
        public float course;
        public float climb;
        public float bank;
        public float turn;
        public float pitchRate;
        public float rollRate;
        public float speed;
        public bool fire;
        public int target;
        public float fireFor;
        public bool burning;
        public bool smoking;
        public float hitAge;
        public int debris;
        public float aimLag;
        public float fireInterval;
        public float flashScale;
        public float overshoot;
        public bool sharpProp;
        public bool boost;
        public bool rearGun;
        public float[] aimOffset;
        public bool noFill;
        public float streakWidth;
        public float[] smokeColor;
        public float smokeScale;
        public float smokeDepth;
        public int hitEvery;
    }

    [Serializable]
    public class ShotShip
    {
        public string kind;
        public float screenX;
        public float distance;
        public float speed;
        public float[] volleys;
        public int[] guns;
        public bool shells;
    }

    [Serializable]
    public class ShotBunker
    {
        public int model;
        public float screenX;
        public float distance;
        public float yaw;
        public float sink;
    }

    [Serializable]
    public class ShotBoat
    {
        public bool wreck;
        public float screenX;
        public float distance;
        public float yaw;
        public float roll;
        public bool capsized;
        public float sink;
        public float scale;
    }

    [Serializable]
    public class ShotGroyne
    {
        public float screenX;
        public float distance;
        public float angle;
    }

    [Serializable]
    public class ShotGull
    {
        public float[] screen;
        public float distance;
        public int count;
        public float spread;
        public float course;
        public float bank;
    }

    [Serializable]
    public class ShotSquad
    {
        public float screenX;
        public float distance;
        public int count;
        public float spread;
        public int faction;
        public bool fire;
        public int target;
        public bool atSquad;
        public int cover;
        public float coverScale;
        public string[] coverModels;
        public float[] coverTint;
        public float coverAngle;
        public bool unarmed;
    }

    [Serializable]
    public class ShotProp
    {
        public string model;
        public string kind;
        public float screenX;
        public float distance;
        public float yaw;
        public float scale;
        public float sink;
        public float pitch;
        public float tilt;
        public bool burning;
        public float smokeScale;
    }

    [Serializable]
    public class ShotBurst
    {
        public float[] screen;
        public float distance;
        public float size;
        public float age;
        public bool ground;
    }

    [Serializable]
    public class ShotZeppelin
    {
        public float[] screen;
        public float distance;
        public float course;
        public float length;
        public float speed;
    }

    [Serializable]
    public class ShotSmoke
    {
        public float screenX;
        public float distance;
        public float scale;
        public bool dense;
    }

    [Serializable]
    public class ShotCrater
    {
        public float screenX;
        public float distance;
        public float radius;
        public float depth;
    }

    [Serializable]
    public class ShotCloud
    {
        public float[] screen;
        public float distance;
        public float width;
        public float alpha;
    }

    [Serializable]
    public class ShotFill
    {
        public float intensity;
        public float yaw;
        public float pitch;
        public float[] color;
    }

    [Serializable]
    public class ShotMistSpec
    {
        public float strength;
        public float top;
        public float clear;
        public float near;
        public float far;
        public float nearShare;
        public float patchSize;
        public float patchiness;
        public float whiten;
    }
}
