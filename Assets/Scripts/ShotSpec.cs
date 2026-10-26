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
        public float clearNear;
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
        public bool burning;
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
}
