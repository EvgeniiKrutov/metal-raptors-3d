using UnityEngine;

namespace MetalRaptors
{
    public class CutsceneVanish : MonoBehaviour
    {
        public const float FadeSec = 0.5f;

        Vector3 _rest;
        float _age;
        System.Action _done;

        public static bool Begin(GameObject target, System.Action done)
        {
            if (target == null || target.GetComponent<CutsceneVanish>() != null) return false;

            var vanish = target.AddComponent<CutsceneVanish>();
            vanish._rest = target.transform.localScale;
            vanish._done = done;
            return true;
        }

        void Update()
        {
            _age += CutscenePause.Delta;
            float k = 1f - Mathf.SmoothStep(0f, 1f, _age / FadeSec);
            if (k > 0f)
            {
                transform.localScale = _rest * k;
                return;
            }

            enabled = false;
            System.Action done = _done;
            _done = null;
            if (done != null) done();
            else Destroy(gameObject);
        }
    }
}
