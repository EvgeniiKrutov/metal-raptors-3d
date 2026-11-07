using System.Collections;
using UnityEngine;

namespace MetalRaptors
{
    public struct FinaleStage
    {
        public Vector3 camera;
        public float distance;
        public float halfViewHeight;
        public float minX, maxX;
        public float floorY, ceilY;
        public EnemyController wreck;
        public UboatFlotilla flotilla;
        public int seed;
        public System.Action<float> shake;
        public System.Action finished;
    }

    public class UboatFinale : MonoBehaviour
    {
        const float FollowResponse = 3.2f;
        const float FollowLimitSec = 15f;
        const float PanSec = 1.8f;
        const float SettleSec = 1.6f;
        const float HoldSec = 5f;
        const float PushIn = 0.12f;
        const float FrameLift = 0.2f;
        const float BlastSize = 260f;

        FinaleStage _stage;
        Vector3 _deck;

        public Vector3 CameraPosition { get; private set; }

        public static UboatFinale Begin(FinaleStage stage)
        {
            var finale = new GameObject("U-Boat Finale").AddComponent<UboatFinale>();
            finale._stage = stage;
            finale.CameraPosition = stage.camera;
            float aim = stage.wreck != null ? stage.wreck.transform.position.x : stage.camera.x;
            finale._deck = stage.flotilla != null ? stage.flotilla.DeckNear(aim) : stage.camera;
            finale.StartCoroutine(finale.Play());
            return finale;
        }

        Vector3 Frame(Vector3 point, float drop) => new Vector3(
            Mathf.Clamp(point.x, _stage.minX, _stage.maxX),
            Mathf.Clamp(point.y - drop, _stage.floorY, _stage.ceilY),
            _stage.camera.z);

        Vector3 UboatShot(float push)
        {
            Vector3 shot = Frame(new Vector3(_deck.x, _stage.floorY + _stage.halfViewHeight * FrameLift, 0f),
                0f);
            shot.z += _stage.distance * PushIn * push;
            return shot;
        }

        IEnumerator Play()
        {
            bool landed = false;
            EnemyController wreck = _stage.wreck;
            bool steered = wreck != null && wreck.SteerFall(_deck, () => landed = true);

            if (steered)
            {
                float left = FollowLimitSec;
                while (!landed && wreck != null && left > 0f)
                {
                    left -= Time.deltaTime;
                    float t = 1f - Mathf.Exp(-FollowResponse * Time.deltaTime);
                    CameraPosition = Vector3.Lerp(CameraPosition, Frame(wreck.transform.position, 0f), t);
                    yield return null;
                }
            }
            else
            {
                Vector3 from = CameraPosition;
                float t = 0f;
                while (t < PanSec)
                {
                    t += Time.deltaTime;
                    CameraPosition = Vector3.Lerp(from, UboatShot(0f), Mathf.SmoothStep(0f, 1f, t / PanSec));
                    yield return null;
                }
            }

            Ignite();

            Vector3 start = CameraPosition;
            float settle = 0f;
            while (settle < SettleSec)
            {
                settle += Time.deltaTime;
                CameraPosition = Vector3.Lerp(start, UboatShot(0f), Mathf.SmoothStep(0f, 1f, settle / SettleSec));
                yield return null;
            }

            float hold = 0f;
            while (hold < HoldSec)
            {
                hold += Time.deltaTime;
                float k = Mathf.Clamp01(hold / HoldSec);
                CameraPosition = UboatShot(1f - (1f - k) * (1f - k));
                yield return null;
            }

            _stage.finished?.Invoke();
        }

        void Ignite()
        {
            Explosion.Spawn(_deck, BlastSize);
            _stage.shake?.Invoke(1f);

            float size = _stage.flotilla != null ? _stage.flotilla.Length : BlastSize;
            PhosphorusFire.Begin(new Vector3(_deck.x, SeaSurface.Level, _deck.z), size, _stage.seed);
        }
    }
}
