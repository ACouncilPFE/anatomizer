using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The cockpit for flat-screen play. Press V to cycle:
    ///   Window      a picture-in-picture of the cabin (CockpitSet): the crew at work, with the
    ///               windshield showing the live view from a camera on the ship's nose.
    ///   Large       the same, bigger.
    ///   Pilot view  you fly from the pilot's seat: a camera on the nose fills the screen inside
    ///               a canopy frame (no middle divider), with a strip showing the crew along the
    ///               bottom. The crosshair and labels line up with this view.
    ///   Off         back to the chase camera only.
    /// In VR you are already sitting in the cockpit, so this switches itself off.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public class CockpitView : MonoBehaviour
    {
        public enum Mode
        {
            Window,
            Large,
            PilotView,
            Off
        }

        [SerializeField] private Mode mode = Mode.Window;
        [SerializeField] private Vector2Int resolution = new Vector2Int(960, 540);
        [SerializeField, Range(0.15f, 0.9f)] private float windowWidthFraction = 0.42f;
        [SerializeField, Range(0.15f, 0.9f)] private float largeWidthFraction = 0.66f;
        [SerializeField] private float pilotFieldOfView = 95f;

        private static readonly Vector3 EyePosition = new Vector3(0f, 0.15f, 1.05f);

        private RenderTexture noseView;
        private RenderTexture cabinView;
        private Camera noseCamera;   // feeds the cabin windshield
        private Camera eyeCamera;    // pilot view, straight to the screen
        private Camera cabinCamera;
        private Camera chaseCamera;
        private CockpitSet set;
        private bool toggleHeld;
        private GUIStyle titleStyle;
        private Texture2D canopy;

        public Mode CurrentMode => mode;

        private void Start()
        {
            if (ShipInput.IsXRActive)
            {
                enabled = false;
                return;
            }

            noseView = new RenderTexture(resolution.x, resolution.y, 24) { name = "NoseView" };
            cabinView = new RenderTexture(resolution.x, resolution.y, 24) { name = "CabinView", antiAliasing = 2 };

            noseCamera = MakeShipCamera("Nose Camera", 100f);
            noseCamera.targetTexture = noseView;

            eyeCamera = MakeShipCamera("Pilot Eye Camera", pilotFieldOfView);
            eyeCamera.depth = 5f;
            eyeCamera.enabled = false;

            // The cabin set, far away from everything else.
            set = new GameObject("Cockpit Set").AddComponent<CockpitSet>();
            set.Build(noseView, GetComponent<ShipController>());

            GameObject cabin = new GameObject("Cockpit Camera");
            cabin.transform.SetParent(set.CameraMount, false);
            cabinCamera = cabin.AddComponent<Camera>();
            cabinCamera.fieldOfView = 72f;
            cabinCamera.nearClipPlane = 0.05f;
            cabinCamera.farClipPlane = 12f; // sees only the set
            cabinCamera.clearFlags = CameraClearFlags.SolidColor;
            cabinCamera.backgroundColor = Color.black;
            cabinCamera.targetTexture = cabinView;

            CameraFollow follow = FindObjectOfType<CameraFollow>();
            chaseCamera = follow != null ? follow.GetComponent<Camera>() : Camera.main;

            canopy = BuildCanopyFrame(512, 288);
            ApplyMode();
        }

        private Camera MakeShipCamera(string name, float fov)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = EyePosition;
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 360f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            return cam;
        }

        private void OnDestroy()
        {
            if (noseView != null) noseView.Release();
            if (cabinView != null) cabinView.Release();
            if (set != null) Destroy(set.gameObject);
        }

        private void Update()
        {
            bool toggle = ShipInput.Read().ToggleCockpitView;
            if (toggle && !toggleHeld)
            {
                mode = (Mode)(((int)mode + 1) % 4);
                ApplyMode();
            }
            toggleHeld = toggle;

            // A little shake when the hull hits the wall.
            ShipPilot pilot = GetComponent<ShipPilot>();
            bool shaking = pilot != null && pilot.IsHoldingWall;
            if (cabinCamera != null)
            {
                cabinCamera.transform.localPosition = shaking ? Random.insideUnitSphere * 0.015f : Vector3.zero;
            }
            if (eyeCamera != null)
            {
                eyeCamera.transform.localPosition = EyePosition + (shaking ? Random.insideUnitSphere * 0.01f : Vector3.zero);
            }

            // In the pilot view the windshield feed only matters for the small crew strip, so
            // refresh it every few frames instead of every frame.
            if (mode == Mode.PilotView && noseCamera != null && Time.frameCount % 3 == 0)
            {
                noseCamera.Render();
            }
        }

        private void ApplyMode()
        {
            bool on = mode != Mode.Off;
            bool pilotView = mode == Mode.PilotView;

            if (noseCamera != null) noseCamera.enabled = on && !pilotView;
            if (cabinCamera != null) cabinCamera.enabled = on;
            if (set != null) set.gameObject.SetActive(on);

            // In the pilot view the eye camera becomes the main camera, so the crosshair and the
            // labels on cells and wall damage line up with what you see. The chase camera stops
            // rendering (its audio listener stays on).
            if (eyeCamera != null)
            {
                eyeCamera.enabled = pilotView;
                eyeCamera.tag = pilotView ? "MainCamera" : "Untagged";
            }
            if (chaseCamera != null)
            {
                chaseCamera.enabled = !pilotView;
                chaseCamera.tag = pilotView ? "Untagged" : "MainCamera";
            }
        }

        private void OnGUI()
        {
            if (mode == Mode.Off || cabinView == null)
            {
                return;
            }

            GUI.depth = 10; // under the HUD, which stays readable on top
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };

            if (mode == Mode.PilotView)
            {
                DrawPilotView();
                return;
            }

            float width = Screen.width * (mode == Mode.Large ? largeWidthFraction : windowWidthFraction);
            float height = width * resolution.y / resolution.x;
            Rect rect = new Rect(Screen.width - width - 16f, Screen.height - height - 16f, width, height);

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(rect.x - 3f, rect.y - 22f, rect.width + 6f, rect.height + 25f), Texture2D.whiteTexture);
            GUI.color = new Color(0.6f, 0.9f, 1f);
            GUI.Label(new Rect(rect.x + 4f, rect.y - 20f, rect.width, 18f),
                mode == Mode.Large ? "COCKPIT  [V] fly from the pilot's seat" : "COCKPIT  [V] bigger", titleStyle);

            GUI.color = Color.white;
            GUI.DrawTexture(rect, cabinView, ScaleMode.ScaleAndCrop, false);
        }

        // Canopy frame over the full-screen view, plus the crew strip along the bottom.
        private void DrawPilotView()
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), canopy, ScaleMode.StretchToFill, true);

            float width = Screen.width * 0.3f;
            float height = width * 0.42f;
            Rect strip = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 6f, width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(strip.x - 3f, strip.y - 20f, strip.width + 6f, strip.height + 23f), Texture2D.whiteTexture);
            GUI.color = new Color(0.6f, 0.9f, 1f);
            GUI.Label(new Rect(strip.x + 4f, strip.y - 19f, strip.width, 18f), "CREW  [V] chase view", titleStyle);
            GUI.color = Color.white;
            // The lower part of the cabin shot: the crew rather than the windshield.
            GUI.DrawTextureWithTexCoords(strip, cabinView, new Rect(0.05f, 0.08f, 0.9f, 0.55f));
        }

        // Dark, slightly metallic edges with a soft rounded opening: the canopy around the view.
        private static Texture2D BuildCanopyFrame(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CanopyFrame" };
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Distance outside a rounded rectangle covering most of the screen.
                    float u = (x / (float)(w - 1)) * 2f - 1f;
                    float v = (y / (float)(h - 1)) * 2f - 1f;
                    float dx = Mathf.Max(Mathf.Abs(u) - 0.86f, 0f) / 0.14f;
                    float dy = Mathf.Max(Mathf.Abs(v) - 0.80f, 0f) / 0.20f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float frame = Mathf.SmoothStep(0.55f, 0.75f, d);
                    float vignette = Mathf.Clamp01((u * u + v * v - 0.6f) * 0.35f);
                    float shade = Mathf.Lerp(0.07f, 0.14f, y / (float)h);
                    float alpha = Mathf.Max(frame, vignette);
                    pixels[y * w + x] = new Color(shade, shade * 1.05f, shade * 1.15f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
