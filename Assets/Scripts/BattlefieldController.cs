using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetalRaptors
{
    /// <summary>
    /// Battlefield scene: a 2.5D combat placeholder. Spawns a spinning 3D prop (proving
    /// the 3D-objects-in-2D setup), shows which mech was deployed from the Garage, and
    /// offers a Back-to-Menu button.
    /// </summary>
    public class BattlefieldController : MonoBehaviour
    {
        Transform _prop;

        void Start()
        {
            _prop = UIFactory.CreatePlaceholderProp(PrimitiveType.Cube, new Vector3(0, 1, 0),
                new Color(0.85f, 0.24f, 0.16f), 2.5f);

            var canvas = UIFactory.CreateCanvas("Battlefield Canvas");

            UIFactory.CreateText(canvas.transform, "BATTLEFIELD", 72,
                new Vector2(0, 420), new Vector2(1000, 120), TextAnchor.MiddleCenter, FontStyle.Bold);

            string mech = GameManager.Instance != null ? GameManager.Instance.SelectedMech : "Unknown";
            UIFactory.CreateText(canvas.transform, $"Deployed: {mech}", 36,
                new Vector2(0, 330), new Vector2(1200, 60));

            UIFactory.CreateButton(canvas.transform, "BACK TO MENU", new Vector2(0, -420),
                () => SceneManager.LoadScene(SceneNames.MainMenu));
        }

        void Update()
        {
            if (_prop != null)
                _prop.Rotate(new Vector3(15, 40, 0) * Time.deltaTime);
        }
    }
}
