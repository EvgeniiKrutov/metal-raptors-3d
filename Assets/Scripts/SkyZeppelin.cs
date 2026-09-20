using System.Collections.Generic;
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

        // How much of the airship's length has to be past the map's left edge before the next
        // one is put in off the right.
        const float Handover = 0.7f;

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

        class Airship
        {
            public Transform tr;
            public float speed, length, halfWindow;
        }

        readonly List<Airship> _ships = new List<Airship>();
        float _leftEdge = float.NegativeInfinity;

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

        // The map's left edge: the line an airship has to be `Handover` past before the next
        // one is sent in. Unset, each airship uses the left edge of its own window instead.
        public void SetLeftEdge(float x) => _leftEdge = x;

        void LateUpdate()
        {
            if (_cam == null) return;

            Vector3 eye = _cam.transform.position;
            Drift(eye);
            Consider(eye);
        }

        void Drift(Vector3 eye)
        {
            for (int i = _ships.Count - 1; i >= 0; i--)
            {
                Airship ship = _ships[i];
                if (ship.tr == null) { _ships.RemoveAt(i); continue; }

                Vector3 pos = ship.tr.position;
                pos.x += ship.speed * Time.deltaTime;
                ship.tr.position = pos;

                if (pos.x - eye.x > -ship.halfWindow - ship.length * HideMargin) continue;

                Destroy(ship.tr.gameObject);
                _ships.RemoveAt(i);
            }
        }

        void Consider(Vector3 eye)
        {
            if (_ships.Count > 0 && !HandedOver(_ships[_ships.Count - 1], eye)) return;

            Spawn(eye, onScreen: false);
        }

        // The airship spans `length` about its own X, so `Handover` of it is past `edge` once
        // its centre is `Handover - 0.5` lengths beyond it.
        bool HandedOver(Airship ship, Vector3 eye)
        {
            float edge = _leftEdge > float.NegativeInfinity
                ? _leftEdge
                : eye.x - ship.halfWindow;

            return ship.tr.position.x <= edge - (Handover - 0.5f) * ship.length;
        }

        void Spawn(Vector3 eye, bool onScreen)
        {
            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"SkyZeppelin: {ModelResource} not found in Resources.");
                enabled = false;
                return;
            }

            float z = _playPlaneZ + CompanionFlight.Depth
                      + Random.Range(DepthBehindDuelMin, DepthBehindDuelMax);
            float grade = (z - eye.z) / _cameraDistance;

            float length = ApparentLength * grade
                           * Random.Range(1f - LengthJitter, 1f + LengthJitter);
            float halfWindow = _halfViewWidth * grade;

            float x = eye.x + (onScreen
                ? halfWindow * Random.Range(OnScreenMin, OnScreenMax)
                : halfWindow + length * HideMargin);

            // The one that opens the level is the only one drawn inside the window, and never
            // past the map's left edge — it would be handed over the moment it appeared.
            if (onScreen) x = Mathf.Max(x, _leftEdge);

            var root = new GameObject("Zeppelin");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(
                x,
                eye.y + _halfViewHeight * grade * Random.Range(RiseMin, RiseMax),
                z);

            var model = Instantiate(prefab, root.transform, false);
            model.name = "zeppelin";
            model.transform.localRotation = NoseWest;

            Fit(model.transform, length);
            Dress(model);
            StartPropellers(model.transform, root.transform);

            _ships.Add(new Airship
            {
                tr = root.transform,
                speed = -Random.Range(SpeedMin, SpeedMax),
                length = length,
                halfWindow = halfWindow,
            });
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
