using UnityEngine;

namespace MetalRaptors
{
    public class SkyFlak : MonoBehaviour
    {
        const float StartDelayMin = 6f, StartDelayMax = 16f;
        const float GapMin = 7f, GapMax = 15f;
        const float StaggerMin = 0.12f, StaggerMax = 0.55f;
        const int SalvoMin = 3, SalvoMax = 5;
        const float TwoSoundChance = 0.4f;

        const float ZMin = -60f, ZMax = 480f;
        const float SpreadX = 1.05f, SpreadY = 1f;
        const float AimedChance = 0.6f;
        const float AimedSpread = 0.4f;
        const float GroundClearance = 45f;

        const float SizeMin = 40f, SizeMax = 80f;

        const float LaneDepth = 90f;
        const float KeepOut = 110f;
        const int PlacementTries = 4;

        Camera _cam;
        Transform _player;
        float _halfViewWidth, _halfViewHeight;
        float _playZ;
        float _intensity = 1f;
        float _timer;
        int _salvoLeft;
        int _soundsLeft;

        public static SkyFlak Begin(Camera cam, Transform player, float halfViewWidth,
            float halfViewHeight, float playPlaneZ, float intensity)
        {
            if (cam == null || intensity <= 0f) return null;

            var flak = new GameObject("Sky Flak").AddComponent<SkyFlak>();
            flak._cam = cam;
            flak._player = player;
            flak._halfViewWidth = halfViewWidth;
            flak._halfViewHeight = halfViewHeight;
            flak._playZ = playPlaneZ;
            flak._intensity = intensity;
            flak._timer = Random.Range(StartDelayMin, StartDelayMax) / intensity;
            return flak;
        }

        void LateUpdate()
        {
            if (_cam == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            if (_salvoLeft <= 0)
            {
                _salvoLeft = Random.Range(SalvoMin, SalvoMax + 1);
                _soundsLeft = Random.value < TwoSoundChance ? 2 : 1;
            }

            Fire();

            _salvoLeft--;
            _timer = _salvoLeft > 0
                ? Random.Range(StaggerMin, StaggerMax)
                : Random.Range(GapMin, GapMax) / _intensity;
        }

        void Fire()
        {
            if (!Place(out Vector3 point, out float size)) return;

            bool sound = _soundsLeft > 0 && Random.Range(0, Mathf.Max(1, _salvoLeft)) < _soundsLeft;
            if (sound) _soundsLeft--;

            FlakBurst.Spawn(point, size, _cam.transform.position, sound);
        }

        bool Place(out Vector3 point, out float size)
        {
            point = Vector3.zero;
            size = Random.Range(SizeMin, SizeMax);

            Vector3 camPos = _cam.transform.position;
            float camDistance = Mathf.Max(1f, _playZ - camPos.z);

            for (int attempt = 0; attempt < PlacementTries; attempt++)
            {
                float z = Random.Range(ZMin, ZMax);
                float scale = Mathf.Max(0.1f, z - camPos.z) / camDistance;

                float x = camPos.x + Random.Range(-1f, 1f) * _halfViewWidth * scale * SpreadX;
                float y = PickY(camPos.y, _halfViewHeight * scale);

                var field = Battlefield.Current;
                if (field != null && field.SampleGround(x, z, out float ground))
                    y = Mathf.Max(y, ground + GroundClearance);

                if (_player != null && Mathf.Abs(z - _playZ) < LaneDepth)
                {
                    float dx = x - _player.position.x;
                    float dy = y - _player.position.y;
                    if (dx * dx + dy * dy < KeepOut * KeepOut) continue;
                }

                point = new Vector3(x, y, z);
                return true;
            }

            return false;
        }

        float PickY(float camY, float span)
        {
            if (_player != null && Random.value < AimedChance)
                return _player.position.y + Random.Range(-1f, 1f) * span * AimedSpread;

            return camY + Random.Range(-1f, 1f) * span * SpreadY;
        }
    }
}
