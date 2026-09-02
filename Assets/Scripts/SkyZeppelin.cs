using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class SkyZeppelin : MonoBehaviour
    {
        const string ModelResource = "objects/machines/zeppelin";

        const float DepthBehindDuelMin = 50f;
        const float DepthBehindDuelMax = 120f;

        const float ApparentLength = 560f;
        const float LengthJitter = 0.12f;

        const float RiseMin = 0.35f, RiseMax = 0.70f;

        const float SpeedMin = 10f, SpeedMax = 20f;

        const float PropSpinMin = 380f, PropSpinMax = 520f;

        const float HideMargin = 0.6f;
        const float OnScreenMin = -0.3f, OnScreenMax = 0.6f;

        static readonly Quaternion NoseWest = Quaternion.Euler(-90f, -90f, 0f);

        static readonly string[] PropPivots =
        {
            "outrigger_forward_port_prop", "outrigger_forward_starboard_prop",
            "outrigger_aft_port_prop", "outrigger_aft_starboard_prop",
        };

        static readonly string[] PropBlades =
            { "front_prop_2", "front_prop_1", "back_prop_2", "back_prop_1" };

        Camera _cam;
        float _halfViewWidth, _halfViewHeight;
        float _playPlaneZ, _cameraDistance;

        Transform _airship;
        float _speed;
        float _length;
        float _halfWindow;
        int _spawnedChunk = int.MinValue;

        public static SkyZeppelin Begin(Camera cam, float halfViewWidth, float halfViewHeight,
            float playPlaneZ, float cameraDistance, bool wanted)
        {
            if (cam == null || !wanted) return null;

            var sky = new GameObject("Sky Zeppelin").AddComponent<SkyZeppelin>();
            sky._cam = cam;
            sky._halfViewWidth = halfViewWidth;
            sky._halfViewHeight = halfViewHeight;
            sky._playPlaneZ = playPlaneZ;
            sky._cameraDistance = Mathf.Max(1f, cameraDistance);

            sky.Spawn(cam.transform.position, onScreen: true);
            return sky;
        }

        void LateUpdate()
        {
            if (_cam == null) return;

            Vector3 eye = _cam.transform.position;
            Drift(eye);
            Consider(eye);
        }

        void Drift(Vector3 eye)
        {
            if (_airship == null) return;

            _airship.position += new Vector3(_speed * Time.deltaTime, 0f, 0f);

            if (_airship.position.x - eye.x > -_halfWindow - _length * HideMargin) return;

            Destroy(_airship.gameObject);
            _airship = null;
        }

        void Consider(Vector3 eye)
        {
            if (_airship != null || ChunkAt(eye.x) == _spawnedChunk) return;

            Spawn(eye, onScreen: false);
        }

        static int ChunkAt(float x) => Mathf.FloorToInt(x / CampaignTerrain.ChunkLength);

        void Spawn(Vector3 eye, bool onScreen)
        {
            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"SkyZeppelin: {ModelResource} not found in Resources.");
                enabled = false;
                return;
            }

            _spawnedChunk = ChunkAt(eye.x);

            float z = _playPlaneZ + CompanionFlight.Depth
                      + Random.Range(DepthBehindDuelMin, DepthBehindDuelMax);
            float grade = (z - eye.z) / _cameraDistance;

            _length = ApparentLength * grade
                      * Random.Range(1f - LengthJitter, 1f + LengthJitter);
            _halfWindow = _halfViewWidth * grade;

            _speed = -Random.Range(SpeedMin, SpeedMax);

            float x = onScreen
                ? _halfWindow * Random.Range(OnScreenMin, OnScreenMax)
                : _halfWindow + _length * HideMargin;

            var root = new GameObject("Zeppelin");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(
                eye.x + x,
                eye.y + _halfViewHeight * grade * Random.Range(RiseMin, RiseMax),
                z);

            var model = Instantiate(prefab, root.transform, false);
            model.name = "zeppelin";
            model.transform.localRotation = NoseWest;

            Fit(model.transform, _length);
            Dress(model);
            StartPropellers(model.transform, root.transform);

            _airship = root.transform;
        }

        static void Fit(Transform model, float length)
        {
            if (!Measure(model, out Bounds bounds)) return;
            if (bounds.size.x > 0.0001f) model.localScale *= length / bounds.size.x;

            if (!Measure(model, out bounds)) return;
            model.localPosition -= model.parent.InverseTransformPoint(bounds.center);
        }

        static bool Measure(Transform model, out Bounds bounds)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            bounds = new Bounds(model.position, Vector3.zero);
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        static void Dress(GameObject model)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            foreach (Collider col in model.GetComponentsInChildren<Collider>())
                Destroy(col);
        }

        static void StartPropellers(Transform model, Transform hull)
        {
            float degrees = Random.Range(PropSpinMin, PropSpinMax);

            for (int i = 0; i < PropPivots.Length; i++)
            {
                Transform spinner = PlaneFactory.FindDeep(model, PropPivots[i])
                                    ?? PlaneFactory.FindDeep(model, PropBlades[i]);
                if (spinner == null)
                {
                    Debug.LogWarning($"SkyZeppelin: {ModelResource} has neither {PropPivots[i]} "
                                     + $"nor {PropBlades[i]}; that propeller cannot spin.");
                    continue;
                }

                var spin = spinner.gameObject.AddComponent<PropellerSpin>();
                spin.axisSpace = hull;
                spin.axisInSpace = Vector3.right;
                spin.degreesPerSecond = degrees;
            }
        }
    }
}
