using UnityEngine;

namespace MetalRaptors
{
    public class SupplyDrop : MonoBehaviour
    {
        CubeController _plane;
        Transform _model;
        SupplyCrate _crate;

        float _z;
        float _floorY;
        float _fraction;
        float _heal;
        int _left;
        int _armed;
        bool _scripted;

        public static SupplyDrop Begin(GameObject owner, CampaignDefinition level,
            CubeController plane, Transform model, float z, float floorY)
        {
            if (level == null || plane == null || level.supplyDrops <= 0) return null;

            var drop = owner.AddComponent<SupplyDrop>();
            drop._plane = plane;
            drop._model = model;
            drop._z = z;
            drop._floorY = floorY;
            drop._fraction = Mathf.Clamp01(level.supplyHealthFraction);
            drop._heal = level.supplyHeal;
            drop._left = level.supplyDrops;
            return drop;
        }

        public int Left => _left + (_crate != null ? 1 : 0);

        public int Armed => _armed + (_crate != null ? 1 : 0);

        public void Restore(int left, int armed)
        {
            _left = Mathf.Max(0, left);
            _armed = Mathf.Max(0, armed);
        }

        public void TakeScriptControl()
        {
            _scripted = true;
            _armed = 0;
        }

        public void Arm(int crates)
        {
            if (!_scripted) return;
            _armed = Mathf.Max(0, crates);
        }

        public void StandDown()
        {
            _left = 0;
            if (_crate != null) Destroy(_crate.gameObject);
            _crate = null;
        }

        public void Tick(Vector3 camPos, float halfWidth, float halfHeight, bool cinematic)
        {
            if (_crate != null)
            {
                _crate.Tick(camPos.x, halfWidth, Time.deltaTime);
                return;
            }

            if (_left <= 0 || (_scripted && _armed <= 0) || cinematic || _plane == null) return;
            if (_plane.CurrentHealth <= 0f) return;
            if (_plane.CurrentHealth > _plane.MaxHealth * _fraction) return;

            _left--;
            if (_scripted) _armed--;
            _crate = SupplyCrate.Spawn(camPos, halfWidth, halfHeight, _z, _floorY,
                _plane.transform, Collect);
        }

        void Collect()
        {
            if (_plane != null) _plane.Heal(_heal);
            HealFlash.Play(_model);
        }
    }
}
