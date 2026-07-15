using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// Garage scene: pick one of three differently coloured cubes (shown as live rotating 3D
    /// previews) and adjust master volume. The chosen cube is persisted via the GameManager,
    /// and its colour becomes the player cube in Level 1 / Level 2. Includes volume controls
    /// and a Back-to-Menu button. All UI is built at runtime.
    /// </summary>
    public class GarageController : MonoBehaviour
    {
        Text _selectedLabel;
        Text _volumeLabel;

        Transform[] _previews;
        Renderer[] _previewRenderers;

        void Start()
        {
            // Use a dark camera clear colour (not a full-screen UI image) so the 3D preview
            // cubes are visible behind the screen-space overlay UI.
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
            }

            var canvas = UIFactory.CreateCanvas("Garage Canvas");

            UIFactory.CreateText(canvas.transform, "GARAGE", 84,
                new Vector2(0, 380), new Vector2(900, 140), TextAnchor.MiddleCenter, FontStyle.Bold);

            _selectedLabel = UIFactory.CreateText(canvas.transform, "", 40,
                new Vector2(0, 250), new Vector2(1200, 60));

            BuildCubePreviews();
            BuildSelectButtons(canvas.transform);
            BuildVolumeControls(canvas.transform);

            UIFactory.CreateButton(canvas.transform, "BACK TO MENU", new Vector2(0, -430),
                () => SceneManager.LoadScene(SceneNames.MainMenu));

            RefreshSelected();
            RefreshVolume();
        }

        void BuildCubePreviews()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            int count = gm.CubeColors.Length;
            _previews = new Transform[count];
            _previewRenderers = new Renderer[count];

            // Spread the cubes horizontally in front of the scene camera (world space).
            float spacing = 4f;
            float startX = -spacing * (count - 1) / 2f;

            for (int i = 0; i < count; i++)
            {
                var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    new Vector3(startX + i * spacing, 2.4f, 0f), Vector3.one * 1.8f,
                    gm.CubeColors[i], emissive: false, keepCollider: false);
                go.name = $"Preview Cube {i}";
                _previews[i] = go.transform;
                _previewRenderers[i] = go.GetComponent<Renderer>();
            }
        }

        void BuildSelectButtons(Transform parent)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            int count = gm.AvailableMechs.Length;
            float spacing = 360f;
            float startX = -spacing * (count - 1) / 2f;

            for (int i = 0; i < count; i++)
            {
                int index = i; // capture
                var button = UIFactory.CreateButton(parent, gm.AvailableMechs[i],
                    new Vector2(startX + i * spacing, -60), () => { gm.SetSelectedMech(index); RefreshSelected(); },
                    new Vector2(320, 84));
                TintButton(button, gm.CubeColors[i]);
            }
        }

        void BuildVolumeControls(Transform parent)
        {
            _volumeLabel = UIFactory.CreateText(parent, "", 34,
                new Vector2(0, -230), new Vector2(600, 50));

            UIFactory.CreateButton(parent, "-", new Vector2(-160, -310),
                () => ChangeVolume(-0.1f), new Vector2(120, 84));
            UIFactory.CreateButton(parent, "+", new Vector2(160, -310),
                () => ChangeVolume(0.1f), new Vector2(120, 84));
        }

        static void TintButton(Button button, Color color)
        {
            var normal = color * 0.7f; normal.a = 1f;
            var pressed = color * 0.5f; pressed.a = 1f;

            var cb = button.colors;
            cb.normalColor = normal;
            cb.highlightedColor = color;
            cb.pressedColor = pressed;
            cb.selectedColor = normal;
            button.colors = cb;
            var img = button.targetGraphic as Image;
            if (img != null) img.color = cb.normalColor;
        }

        void Update()
        {
            if (_previews == null) return;

            int selected = GameManager.Instance != null ? GameManager.Instance.SelectedMechIndex : 0;
            for (int i = 0; i < _previews.Length; i++)
            {
                if (_previews[i] == null) continue;
                float speed = i == selected ? 90f : 35f;
                _previews[i].Rotate(new Vector3(20f, speed, 0f) * Time.deltaTime);
            }
        }

        void RefreshSelected()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            _selectedLabel.text = $"Selected: {gm.SelectedMech}";

            // Highlight the chosen cube: bigger + glowing, the rest plain.
            if (_previews == null) return;
            for (int i = 0; i < _previews.Length; i++)
            {
                bool isSel = i == gm.SelectedMechIndex;
                if (_previews[i] != null)
                    _previews[i].localScale = Vector3.one * (isSel ? 2.6f : 1.8f);

                var r = _previewRenderers[i];
                if (r == null) continue;
                var mat = r.material;
                if (isSel)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", gm.CubeColors[i] * 1.4f);
                }
                else
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        void ChangeVolume(float delta)
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.SetMasterVolume(GameManager.Instance.MasterVolume + delta);
            RefreshVolume();
        }

        void RefreshVolume()
        {
            if (GameManager.Instance != null)
                _volumeLabel.text = $"Master Volume: {Mathf.RoundToInt(GameManager.Instance.MasterVolume * 100)}%";
        }
    }
}
