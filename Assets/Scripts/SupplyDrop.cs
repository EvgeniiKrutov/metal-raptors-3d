using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class SupplyDrop : MonoBehaviour
    {
        const float MinRoomFraction = 0.3f;
        const int RepairTaps = 5;

        CubeController _plane;
        Transform _model;
        SupplyCrate _crate;

        float _z;
        float _floorY;
        float _fraction;
        float _heal;
        float _lead;
        int _left;
        int _armed;
        bool _scripted;
        bool _bothWays;
        Func<float> _warn;
        bool _incoming;
        float _incomingLeft;
        Vector3 _camPos;
        float _halfWidth;
        float _halfHeight;
        bool _repairs;
        int _repairLeft;
        bool _tapped;

        public static SupplyDrop Begin(GameObject owner, CampaignDefinition level,
            CubeController plane, Transform model, float z, float floorY, Func<float> warn)
        {
            if (level == null || plane == null || level.supplyDrops <= 0) return null;

            var drop = owner.AddComponent<SupplyDrop>();
            drop._plane = plane;
            drop._model = model;
            drop._z = z;
            drop._floorY = floorY;
            drop._fraction = Mathf.Clamp01(level.supplyHealthFraction);
            drop._heal = level.supplyHeal;
            drop._lead = level.supplyLead;
            drop._left = level.supplyDrops;
            drop._bothWays = level.worldWidth > 0f;
            drop._warn = warn;
            drop._repairs = level.supplyRepairs;
            return drop;
        }

        bool Pending => _crate != null || _incoming || Repairing;

        public bool Repairs => _repairs;

        public bool Repairing => _repairLeft > 0;

        public int RepairLeft => _repairLeft;

        public void Tap() => _tapped = true;

        public bool Aim(out Vector3 box)
        {
            if (_crate != null)
            {
                box = _crate.Box;
                return true;
            }

            box = Vector3.zero;
            if (!_incoming || _plane == null) return false;

            box = SupplyCrate.SpawnBox(_camPos, _halfWidth, _halfHeight, _z,
                Side(_camPos.x, _halfWidth), _lead);
            return true;
        }

        public int Left => _left + (Pending ? 1 : 0);

        public int Armed => _armed + (Pending ? 1 : 0);

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
            _incoming = false;
            _repairLeft = 0;
            if (_crate != null) Destroy(_crate.gameObject);
            _crate = null;
        }

        public void Tick(Vector3 camPos, float halfWidth, float halfHeight, bool cinematic)
        {
            _camPos = camPos;
            _halfWidth = halfWidth;
            _halfHeight = halfHeight;

            bool tapped = _tapped || TappedKey();
            _tapped = false;

            if (Repairing)
            {
                if (tapped && !cinematic && --_repairLeft <= 0) Repair();
                return;
            }

            if (_crate != null)
            {
                _crate.Tick(camPos.x, halfWidth, Time.deltaTime);
                return;
            }

            if (_incoming)
            {
                _incomingLeft -= Time.deltaTime;
                if (_incomingLeft > 0f || cinematic || _plane == null) return;

                _incoming = false;
                _crate = SupplyCrate.Spawn(camPos, halfWidth, halfHeight, _z, _floorY,
                    Side(camPos.x, halfWidth), _lead, _plane.transform, Collect);
                return;
            }

            if (_left <= 0 || (_scripted && _armed <= 0) || cinematic || _plane == null) return;
            if (_plane.CurrentHealth <= 0f) return;
            if (_plane.CurrentHealth > _plane.MaxHealth * _fraction) return;
            if (EnemyWarning.Alarming) return;

            _left--;
            if (_scripted) _armed--;
            if (_repairs)
            {
                _repairLeft = RepairTaps;
                return;
            }

            _incoming = true;
            _incomingLeft = _warn != null ? _warn() : 0f;
        }

        bool TappedKey()
        {
            if (!_repairs || GameMenu.IsOpen || LevelBriefing.IsOpen) return false;

            var kb = Keyboard.current;
            return kb != null && kb.hKey.wasPressedThisFrame;
        }

        void Repair()
        {
            Collect();
            if (_plane != null) SupplyCrate.PlayChime(_plane.transform.position);
        }

        float Side(float camX, float halfWidth)
        {
            if (!_bothWays) return LevelScroll.Sign;

            float nose = Mathf.Cos(_plane.Heading) >= 0f ? 1f : -1f;
            float drop = camX + nose * halfWidth * SupplyCrate.LeadFraction;
            float room = (drop - _plane.transform.position.x) * nose;
            return room >= halfWidth * MinRoomFraction ? nose : -nose;
        }

        void Collect()
        {
            if (_plane != null) _plane.Heal(_heal);
            HealFlash.Play(_model);
        }
    }
}
