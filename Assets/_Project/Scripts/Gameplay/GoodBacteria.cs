using AIGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Harmless gut bacteria: green rods drifting slowly along a passage. They do the patient good,
    /// so shooting one is a mistake: it counts as a healthy cell lost (GameEvents.RaiseCellDestroyed
    /// with wasCurable = true), and losing too many fails the mission. Medicine does nothing to them.
    /// </summary>
    public class GoodBacteria : MonoBehaviour, IDamageable
    {
        private static Mesh rodMesh;
        private static Material rodMaterial;
        private static GUIStyle labelStyle;

        private Passage passage;
        private float s;
        private float angle;
        private float radial;
        private float drift;
        private float wobble;
        private float health = 20f;

        public void Configure(Passage onPassage, float atS)
        {
            passage = onPassage;
            s = atS;
            angle = Random.Range(0f, Mathf.PI * 2f);
            radial = Random.Range(0.2f, 0.7f);
            drift = Random.Range(0.3f, 0.9f);
            wobble = Random.value * 10f;

            rodMesh ??= ProceduralMeshes.DisplacedSphere("BacteriumRod", 16, 10, d => 0.5f);
            if (rodMaterial == null)
            {
                rodMaterial = new Material(Shader.Find("Standard")) { name = "GoodBacteria" };
                rodMaterial.color = new Color(0.35f, 0.8f, 0.42f);
                rodMaterial.SetFloat("_Glossiness", 0.6f);
                rodMaterial.EnableKeyword("_EMISSION");
                rodMaterial.SetColor("_EmissionColor", new Color(0.04f, 0.16f, 0.06f));
            }

            GameObject body = new GameObject("Rod");
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.35f, 0.35f, 1.3f);
            body.AddComponent<MeshFilter>().sharedMesh = rodMesh;
            MeshRenderer r = body.AddComponent<MeshRenderer>();
            r.sharedMaterial = rodMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;

            SphereCollider hit = gameObject.AddComponent<SphereCollider>();
            hit.radius = 0.55f;
            transform.rotation = Random.rotation;
            Place();
        }

        public void ApplyDamage(float amount)
        {
            health -= amount;
            if (health > 0f)
            {
                return;
            }

            GameEvents.RaiseCellDestroyed(true); // a healthy cell wasted
            GameAudio.PlayAt(ProceduralAudio.CellPop, transform.position, 0.7f, 1.3f);
            Destroy(gameObject);
        }

        private void Update()
        {
            if (passage == null)
            {
                return;
            }

            s += drift * Time.deltaTime;
            if (s > passage.MaxTravel)
            {
                s = passage.MinTravel;
            }
            angle += Mathf.Sin(Time.time * 0.3f + wobble) * 0.1f * Time.deltaTime;
            Place();
            transform.Rotate(new Vector3(13f, 29f, 7f) * Time.deltaTime, Space.Self);
        }

        private void Place()
        {
            transform.position = passage.PointAt(s, angle, radial);
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.8f);
            if (screen.z <= 0f || screen.z > 22f)
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            Color previous = GUI.color;
            GUI.color = new Color(0.5f, 1f, 0.55f, 0.85f);
            GUI.Label(new Rect(screen.x - 90f, Screen.height - screen.y - 10f, 180f, 20f), "good bacteria - don't shoot", labelStyle);
            GUI.color = previous;
        }
    }
}
