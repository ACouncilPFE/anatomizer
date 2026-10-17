using AIGame.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.AI
{
    public enum CellCondition
    {
        /// <summary>Sick but curable: shoot it with medicine.</summary>
        Infected,
        /// <summary>Too far gone: medicine does nothing, it has to be destroyed.</summary>
        Malignant
    }

    /// <summary>
    /// Makes a bad cell look unhealthy at a glance, and shows which kind it is:
    ///   Infected   swollen, lumpy, sickly yellow-green with pale pustules; slow green pulse.
    ///   Malignant  dark purple-black mass covered in spikes; fast, angry red pulse.
    /// When an infected cell is cured it turns back into an ordinary red blood cell.
    ///
    /// Replaces the mesh and material on the cell's "Mesh" child, so the existing enemy prefabs
    /// need no changes.
    /// </summary>
    [RequireComponent(typeof(EnemyAgent))]
    public class DiseasedCellVisual : MonoBehaviour
    {
        private const float BodyRadius = 0.85f;

        private static Mesh infectedMesh;
        private static Mesh malignantMesh;
        private static Mesh healthyMesh;
        private static Mesh knobMesh;
        private static Material infectedMaterial;
        private static Material malignantMaterial;
        private static Material healthyMaterial;
        private static Material pustuleMaterial;

        private Transform body;
        private Renderer bodyRenderer;
        private Transform extras;
        private MaterialPropertyBlock block;
        private Color pulseColor;
        private float pulseRate;
        private float phase;
        private bool healed;

        public void Apply(CellCondition condition)
        {
            EnsureAssets();
            FindBody();
            if (body == null)
            {
                return;
            }

            healed = false;
            phase = Random.value * Mathf.PI * 2f;
            body.localScale = Vector3.one;
            body.localRotation = Random.rotation;
            ClearExtras();

            MeshFilter filter = body.GetComponent<MeshFilter>();
            if (condition == CellCondition.Malignant)
            {
                filter.sharedMesh = malignantMesh;
                bodyRenderer.sharedMaterial = malignantMaterial;
                pulseColor = new Color(0.22f, 0.01f, 0.05f);
                pulseRate = 4.5f;
            }
            else
            {
                filter.sharedMesh = infectedMesh;
                bodyRenderer.sharedMaterial = infectedMaterial;
                pulseColor = new Color(0.16f, 0.28f, 0.02f);
                pulseRate = 1.8f;
                AddPustules();
            }

            // Hits should land on what you can see.
            SphereCollider hitbox = GetComponent<SphereCollider>();
            if (hitbox != null)
            {
                hitbox.radius = BodyRadius * 1.1f;
            }
        }

        /// <summary>Cured: back to a plain red blood cell, no glow.</summary>
        public void ShowHealed()
        {
            EnsureAssets();
            FindBody();
            if (body == null)
            {
                return;
            }

            healed = true;
            ClearExtras();
            body.localScale = Vector3.one;
            body.GetComponent<MeshFilter>().sharedMesh = healthyMesh;
            bodyRenderer.sharedMaterial = healthyMaterial;
            bodyRenderer.SetPropertyBlock(null);
        }

        private void Update()
        {
            if (healed || bodyRenderer == null)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseRate + phase);
            block ??= new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", pulseColor * Mathf.Lerp(0.15f, 1f, pulse));
            bodyRenderer.SetPropertyBlock(block);

            // A slow, uneven "breathing" so the cells look alive.
            body.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.time * pulseRate * 0.5f + phase));
        }

        private void FindBody()
        {
            if (body != null)
            {
                return;
            }

            MeshFilter filter = GetComponentInChildren<MeshFilter>();
            if (filter == null)
            {
                return;
            }

            body = filter.transform;
            bodyRenderer = filter.GetComponent<Renderer>();
            if (bodyRenderer is MeshRenderer meshRenderer)
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private void AddPustules()
        {
            extras = new GameObject("Pustules").transform;
            extras.SetParent(body, false);
            int count = Random.Range(7, 12);
            for (int i = 0; i < count; i++)
            {
                GameObject knob = new GameObject("Pustule");
                knob.transform.SetParent(extras, false);
                Vector3 dir = Random.onUnitSphere;
                knob.transform.localPosition = dir * BodyRadius * InfectedRadius(dir) * 0.95f;
                knob.transform.localScale = Vector3.one * Random.Range(0.14f, 0.26f);
                knob.AddComponent<MeshFilter>().sharedMesh = knobMesh;
                MeshRenderer r = knob.AddComponent<MeshRenderer>();
                r.sharedMaterial = pustuleMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private void ClearExtras()
        {
            if (extras != null)
            {
                Destroy(extras.gameObject);
                extras = null;
            }
        }

        // ─── Shared meshes and materials (built once) ───────────────────────────────

        private static float InfectedRadius(Vector3 d)
        {
            // Swollen and lumpy: a few overlapping bulges.
            return 1f + 0.10f * Mathf.Sin(3.1f * d.x + 1.7f * d.y)
                      + 0.07f * Mathf.Sin(4.3f * d.y - 2.1f * d.z + 1f)
                      + 0.05f * Mathf.Sin(5.7f * d.z + 2.9f * d.x + 2f);
        }

        private static void EnsureAssets()
        {
            if (infectedMesh != null)
            {
                return;
            }

            infectedMesh = ProceduralMeshes.DisplacedSphere("InfectedCell", 32, 22, d => BodyRadius * InfectedRadius(d));

            Random.State saved = Random.state;
            Random.InitState(1234);
            Vector3[] spikes = new Vector3[16];
            for (int i = 0; i < spikes.Length; i++)
            {
                spikes[i] = Random.onUnitSphere;
            }
            Random.state = saved;
            malignantMesh = ProceduralMeshes.DisplacedSphere("MalignantCell", 56, 40, d =>
            {
                float r = 0.9f + 0.08f * Mathf.Sin(5f * d.x + 3f * d.z) + 0.06f * Mathf.Sin(7f * d.y - 2f * d.x);
                foreach (Vector3 s in spikes)
                {
                    r += 0.7f * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(d, s)), 60f);
                }
                return BodyRadius * r;
            });

            healthyMesh = ProceduralMeshes.BiconcaveDisc(0.75f);
            knobMesh = ProceduralMeshes.DisplacedSphere("Pustule", 12, 8, d => 0.5f);

            infectedMaterial = MakeMaterial("InfectedCell", new Color(0.8f, 0.8f, 0.75f), 0.3f, ProceduralTextures.InfectedCell(128));
            malignantMaterial = MakeMaterial("MalignantCell", new Color(0.85f, 0.8f, 0.9f), 0.25f, ProceduralTextures.MalignantCell(128));
            healthyMaterial = MakeMaterial("HealedCell", new Color(0.74f, 0.06f, 0.10f), 0.55f, null);
            pustuleMaterial = MakeMaterial("Pustule", new Color(0.85f, 0.8f, 0.4f), 0.6f, null);
            pustuleMaterial.SetColor("_EmissionColor", new Color(0.12f, 0.11f, 0.02f));
        }

        private static Material MakeMaterial(string name, Color color, float gloss, Texture2D texture)
        {
            Material mat = new Material(Shader.Find("Standard")) { name = name };
            mat.color = color;
            if (texture != null)
            {
                mat.mainTexture = texture;
            }
            mat.SetFloat("_Glossiness", gloss);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            return mat;
        }
    }
}
