using UnityEngine;

namespace MetalRaptors
{
    public class SkyBomb : MonoBehaviour
    {
        const float AlignResponse = 4f;
        const float BlastSize = 72f;
        const float BoomVolume = 1f;

        Vector2 _velocity;
        float _gravity;
        float _floorY;
        float _damage;
        float _reach;
        float _angle;
        Transform _target;
        IDamageable _victim;
        CubeController _player;
        System.Action _onHit;

        public static SkyBomb Drop(GameObject prop, Vector3 from, Vector2 velocity, float gravity,
            float scale, float floorY, Transform target, float damage, float reach,
            System.Action onHit = null)
        {
            GameObject go = Instantiate(prop, from, Quaternion.identity);
            go.name = "SkyBomb";
            go.transform.localScale = prop.transform.localScale * scale;
            go.SetActive(true);

            var bomb = go.AddComponent<SkyBomb>();
            bomb._velocity = velocity;
            bomb._gravity = Mathf.Max(1f, gravity);
            bomb._floorY = floorY;
            bomb._damage = damage;
            bomb._reach = reach;
            bomb._onHit = onHit;
            bomb._angle = velocity.x < 0f ? 180f : 0f;
            bomb._target = target;
            bomb._victim = target != null ? target.GetComponent<IDamageable>() : null;
            bomb._player = target != null ? target.GetComponent<CubeController>() : null;
            go.transform.rotation = Quaternion.Euler(0f, 0f, bomb._angle);
            return bomb;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _velocity.y -= _gravity * dt;

            Vector3 pos = transform.position + (Vector3)(_velocity * dt);
            transform.position = pos;

            float target = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            _angle = Mathf.LerpAngle(_angle, target, 1f - Mathf.Exp(-AlignResponse * dt));
            transform.rotation = Quaternion.Euler(0f, 0f, _angle);

            if (pos.y < _floorY)
            {
                Destroy(gameObject);
                return;
            }

            if (!Strikes(pos)) return;

            Explosion.Spawn(pos, BlastSize, BoomVolume * AudioOptions.Sfx);
            _victim.TakeDamage(_damage);
            _onHit?.Invoke();
            Destroy(gameObject);
        }

        bool Strikes(Vector3 pos)
        {
            if (_victim == null || _target == null) return false;
            if (_player != null && (_player.Evading || _player.CurrentHealth <= 0f)) return false;

            return ((Vector2)_target.position - (Vector2)pos).sqrMagnitude <= _reach * _reach;
        }
    }
}
