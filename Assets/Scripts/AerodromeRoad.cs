using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class AerodromeRoad : MonoBehaviour
    {
        public const float HalfWidth = 9f * PlaneModelConfig.UnitsPerMeter / 2f;

        const float SampleStep = 14f;
        const float Lift = 1.6f;
        const float Crown = 1.4f;

        static readonly Color RoadColor = new Color(0.52f, 0.47f, 0.39f);

        Mesh _mesh;

        public static AerodromeRoad Build(CampaignTerrain land, float x0, float x1, float z)
        {
            if (land == null || x1 <= x0) return null;

            int spans = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / SampleStep));
            var verts = new Vector3[(spans + 1) * 3];
            var uvs = new Vector2[verts.Length];
            var tris = new int[spans * 4 * 3];

            for (int i = 0; i <= spans; i++)
            {
                float x = Mathf.Lerp(x0, x1, (float)i / spans);
                float u = (x - x0) / (HalfWidth * 2f);

                for (int lane = 0; lane < 3; lane++)
                {
                    float offset = (lane - 1) * HalfWidth;
                    float lift = Lift + (lane == 1 ? Crown : 0f);
                    verts[i * 3 + lane] = new Vector3(x, Ground(land, x, z + offset) + lift,
                        z + offset);
                    uvs[i * 3 + lane] = new Vector2(u, lane * 0.5f);
                }
            }

            for (int i = 0, t = 0; i < spans; i++)
                for (int lane = 0; lane < 2; lane++)
                {
                    int a = i * 3 + lane, b = a + 1, c = a + 3, d = a + 4;
                    tris[t++] = a; tris[t++] = b; tris[t++] = c;
                    tris[t++] = b; tris[t++] = d; tris[t++] = c;
                }

            var mesh = new Mesh { name = "Aerodrome Road (generated)" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Aerodrome Road", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = BattlefieldProps.Layer;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var road = go.AddComponent<AerodromeRoad>();
            road._mesh = mesh;
            return road;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        static float Ground(CampaignTerrain land, float x, float z) =>
            land.SampleHeight(x, z, out float y) ? y : ProceduralTerrain.BaseLevel;

        static Material Material()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", RoadColor);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return mat;
        }
    }
}
