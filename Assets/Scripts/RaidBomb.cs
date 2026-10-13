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
        System.Action<Vector3> _landed;

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

            float fall = 0.5f * _gravity * _age * _age;
            transform.position = _from + _drift * _age + Vector3.down * fall;

            float target = Mathf.Atan2(-_gravity * _age, _drift.x) * Mathf.Rad2Deg;
            _angle = Mathf.LerpAngle(_angle, target, 1f - Mathf.Exp(-AlignResponse * dt));
            transform.rotation = Quaternion.Euler(0f, 0f, _angle);
        }
    }
}
