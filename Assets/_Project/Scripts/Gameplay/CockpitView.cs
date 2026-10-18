using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// A window into the capsule for flat-screen play: a camera behind the crew (CockpitSet), with
    /// the windshield showing the live view from a camera on the ship's nose.
    /// Press V to cycle: window (bottom right) -> large window -> full-screen cockpit -> off.
    /// In VR you are already sitting in the cockpit, so this switches itself off.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public class CockpitView : MonoBehaviour
    {
        public enum Mode
        {
            Window,
            Large,
            FullScreen,
            Off
        }

        [SerializeField] private Mode mode = Mode.Window;
        [SerializeField] private Vector2Int resolution = new Vector2Int(960, 540);
        [SerializeField, Range(0.15f, 0.9f)] private float windowWidthFraction = 0.42f;
        [SerializeField, Range(0.15f, 0.9f)] private float largeWidthFraction = 0.66f;

        private RenderTexture noseView;
        private RenderTexture cabinView;
        private Camera noseCamera;
        private Camera cabinCamera;
        private CockpitSet set;
        private bool toggleHeld;
        private GUIStyle titleStyle;

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

            // Nose camera: what the crew see out of the front.
            GameObject nose = new GameObject("Nose Camera");
            nose.transform.SetParent(transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.15f, 1.05f);
            noseCamera = nose.AddComponent<Camera>();
            noseCamera.fieldOfView = 100f; // wide, to take in as much of the artery wall as possible
            noseCamera.nearClipPlane = 0.05f;
            noseCamera.farClipPlane = 360f;
            noseCamera.targetTexture = noseView;

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

            ApplyMode();
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
            if (cabinCamera != null)
            {
                bool shaking = pilot != null && pilot.IsHoldingWall;
                cabinCamera.transform.localPosition = shaking ? Random.insideUnitSphere * 0.015f : Vector3.zero;
            }
        }

        private void ApplyMode()
        {
            bool on = mode != Mode.Off;
            if (noseCamera != null) noseCamera.enabled = on;
            if (cabinCamera != null) cabinCamera.enabled = on;
            if (set != null) set.gameObject.SetActive(on);
        }

        private void OnGUI()
        {
            if (mode == Mode.Off || cabinView == null)
            {
                return;
            }

            GUI.depth = 10; // under the HUD, which stays readable on top
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };

            Rect rect;
            if (mode == Mode.FullScreen)
            {
                rect = new Rect(0f, 0f, Screen.width, Screen.height);
            }
            else
            {
                float width = Screen.width * (mode == Mode.Large ? largeWidthFraction : windowWidthFraction);
                float height = width * resolution.y / resolution.x;
                rect = new Rect(Screen.width - width - 16f, Screen.height - height - 16f, width, height);

                // Frame.
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(new Rect(rect.x - 3f, rect.y - 22f, rect.width + 6f, rect.height + 25f), Texture2D.whiteTexture);
                GUI.color = new Color(0.6f, 0.9f, 1f);
                GUI.Label(new Rect(rect.x + 4f, rect.y - 20f, rect.width, 18f), mode == Mode.Large ? "COCKPIT  [V] full screen" : "COCKPIT  [V] bigger", titleStyle);
            }

            GUI.color = Color.white;
            GUI.DrawTexture(rect, cabinView, ScaleMode.ScaleAndCrop, false);
        }
    }
}
