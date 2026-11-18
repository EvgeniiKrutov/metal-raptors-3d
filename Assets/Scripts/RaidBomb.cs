using UnityEngine;

namespace MetalRaptors
{
    public class RaidBomb : MonoBehaviour
    {
        const float AlignResponse = 4f;
        const float ReleaseAngle = 180f;

        Vector3 _from;
        Vector3 _to;
        Vector3 _drift;
        float _gravity;
        float _seconds;
        float _age;
        float _angle;
        bool _free;
        float _climb;
        float _yaw;
        float _pitch;
        System.Action<Vector3> _landed;

        public static RaidBomb Fall(GameObject prop, Vector3 from, Vector3 velocity, Vector3 to, float gravity,
            float scale, System.Action<Vector3> landed)
        {
            float g = Mathf.Max(1f, gravity);
            float drop = Mathf.Max(1f, from.y - to.y);
            float seconds = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * g * drop)) / g;
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 natural = from + flat * seconds;
            Vector3 drift = flat + new Vector3(to.x - natural.x, 0f, to.z - natural.z) / seconds;
            float yaw = drift.sqrMagnitude > 1e-4f ? Mathf.Atan2(drift.z, drift.x) : 0f;

            GameObject go = Instantiate(prop, from, Pose(yaw, 0f));
            go.name = "RaidBomb";
            go.transform.localScale = prop.transform.localScale * scale;
            go.SetActive(true);

            var bomb = go.AddComponent<RaidBomb>();
            bomb._free = true;
            bomb._from = from;
            bomb._to = to;
            bomb._gravity = g;
            bomb._seconds = seconds;
            bomb._drift = drift;
            bomb._climb = velocity.y;
            bomb._yaw = yaw;
            bomb._landed = landed;
            return bomb;
        }

        static Quaternion Pose(float yaw, float pitch) =>
            Quaternion.Euler(0f, -yaw * Mathf.Rad2Deg, 0f) * Quaternion.Euler(0f, 0f, pitch);

        void FreeFall(float dt)
        {
            float t = _age;
            transform.position = _from + _drift * t + Vector3.up * (_climb * t - 0.5f * _gravity * t * t);

            float speed = new Vector2(_drift.x, _drift.z).magnitude;
            float target = Mathf.Atan2(_climb - _gravity * t, Mathf.Max(1f, speed)) * Mathf.Rad2Deg;
            _pitch = Mathf.LerpAngle(_pitch, target, 1f - Mathf.Exp(-AlignResponse * dt));
            transform.rotation = Pose(_yaw, _pitch);
        }

        public static RaidBomb Drop(GameObject prop, Vector3 from, Vector3 to, float gravity,
            float scale, System.Action<Vector3> landed)
        {
            GameObject go = Instantiate(prop, from, Quaternion.Euler(0f, 0f, ReleaseAngle));
            go.name = "RaidBomb";
            go.transform.localScale = prop.transform.localScale * scale;
            go.SetActive(true);

            var bomb = go.AddComponent<RaidBomb>();
            bomb._from = from;
            bomb._to = to;
            bomb._gravity = Mathf.Max(1f, gravity);
            bomb._seconds = Mathf.Sqrt(2f * Mathf.Max(1f, from.y - to.y) / bomb._gravity);
            bomb._drift = new Vector3(to.x - from.x, 0f, to.z - from.z) / bomb._seconds;
            bomb._angle = ReleaseAngle;
            bomb._landed = landed;
            return bomb;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;

            if (_age >= _seconds)
            {
                transform.position = _to;
                _landed?.Invoke(_to);
                Destroy(gameObject);
                return;
            }

            if (_free)
            {
                FreeFall(dt);
                return;
            }

            float fall = 0.5f * _gravity * _age * _age;
            transform.position = _from + _drift * _age + Vector3.down * fall;

            float target = Mathf.Atan2(-_gravity * _age, _drift.x) * Mathf.Rad2Deg;
            _angle = Mathf.LerpAngle(_angle, target, 1f - Mathf.Exp(-AlignResponse * dt));
            transform.rotation = Quaternion.Euler(0f, 0f, _angle);
        }
    }
}
