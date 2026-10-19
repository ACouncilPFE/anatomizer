using AIGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The inside of the capsule, built from simple shapes and kept far away from the artery so
    /// only the cockpit camera sees it. Four crew sit inside and react to what the ship is doing:
    ///   Pilot      hands on the stick, which tilts with the steering; leans into turns.
    ///   Co-pilot   turns to track the current target and jerks when firing.
    ///   Surgeon    leans over the beam console while the surgical beam is working.
    ///   Navigator  watches the patient's vitals, glancing up at the windshield now and then.
    /// The windshield shows the live view ahead (a texture from CockpitView's nose camera), the
    /// consoles show live readouts, and a warning light flashes on heavy blood loss or a wall hit.
    /// </summary>
    public class CockpitSet : MonoBehaviour
    {
        /// <summary>Where the set is built: far below the artery, out of every other camera's reach.</summary>
        public static readonly Vector3 Origin = new Vector3(0f, -1000f, 0f);

        private struct CrewMember
        {
            public Transform torso;
            public Transform head;
            public Transform leftArm;
            public Transform rightArm;
        }

        private CrewMember pilot, coPilot, surgeon, navigator;
        private Transform stick;
        private Light alarm;
        private Renderer beamScreen;
        private TextMesh vitalsText, gunnerText, beamText;
        private float nextTextUpdate;
        private float coPilotRecoil;

        private Rigidbody shipBody;
        private Transform ship;
        private ShipPilot shipPilot;
        private ShipCoPilot shipCoPilot;
        private SurgicalBeam beam;

        public Transform CameraMount { get; private set; }

        public void Build(Texture windshieldView, ShipController owner)
        {
            ship = owner.transform;
            shipBody = owner.GetComponent<Rigidbody>();
            shipPilot = owner.GetComponent<ShipPilot>();
            shipCoPilot = owner.GetComponent<ShipCoPilot>();
            beam = owner.GetComponent<SurgicalBeam>();

            transform.position = Origin;
            transform.rotation = Quaternion.identity;

            Material hull = Mat(new Color(0.32f, 0.35f, 0.4f), 0.5f, Color.black, 0.6f);
            Material trim = Mat(new Color(0.15f, 0.16f, 0.19f), 0.4f, Color.black, 0.8f);
            Material floor = Mat(new Color(0.12f, 0.13f, 0.15f), 0.2f, Color.black, 0.3f);
            Material glowCyan = Mat(new Color(0.05f, 0.1f, 0.12f), 0.8f, new Color(0.1f, 0.8f, 1f));
            Material glowAmber = Mat(new Color(0.1f, 0.07f, 0.02f), 0.8f, new Color(1f, 0.6f, 0.15f));
            Material screenGlow = Mat(new Color(0.02f, 0.04f, 0.05f), 0.9f, new Color(0.02f, 0.12f, 0.16f));

            // Hull: an egg-shaped shell seen from the inside, with ribs.
            Transform shell = Part(PrimitiveType.Sphere, "Hull", new Vector3(0f, 1.35f, 0.2f), new Vector3(5.4f, 3.8f, 7f), hull);
            FlipInsideOut(shell.GetComponent<MeshFilter>());
            for (int i = 0; i < 4; i++)
            {
                Part(PrimitiveType.Cube, "Rib", new Vector3(0f, 2.35f, -1.6f + i * 1.15f), new Vector3(2.4f, 0.06f, 0.12f), trim);
            }
            Part(PrimitiveType.Cube, "Floor", new Vector3(0f, 0f, 0.2f), new Vector3(2.8f, 0.05f, 5f), floor);

            // Windshield with the live view ahead, framed.
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Windshield";
            Strip(screen);
            screen.transform.SetParent(transform, false);
            // Big panoramic windshield: most of the view is the artery ahead.
            // A huge windshield filling the front of the cabin: most of what the crew see is artery.
            screen.transform.localPosition = new Vector3(0f, 1.55f, 1.75f);
            screen.transform.localScale = new Vector3(4.9f, 2.9f, 1f);
            screen.GetComponent<Renderer>().sharedMaterial = ScreenMaterial(windshieldView);
            Part(PrimitiveType.Cube, "FrameTop", new Vector3(0f, 3.02f, 1.72f), new Vector3(5.1f, 0.07f, 0.07f), trim);
            Part(PrimitiveType.Cube, "FrameBottom", new Vector3(0f, 0.08f, 1.72f), new Vector3(5.1f, 0.07f, 0.07f), trim);
            Part(PrimitiveType.Cube, "FrameLeft", new Vector3(-2.48f, 1.55f, 1.72f), new Vector3(0.07f, 3f, 0.07f), trim);
            Part(PrimitiveType.Cube, "FrameRight", new Vector3(2.48f, 1.55f, 1.72f), new Vector3(0.07f, 3f, 0.07f), trim);

            // Dashboard with glowing strips.
            // Low dashboard, so it doesn't block the windshield.
            Part(PrimitiveType.Cube, "Dashboard", new Vector3(0f, 0.32f, 1.45f), new Vector3(3.2f, 0.28f, 0.45f), trim);
            Part(PrimitiveType.Cube, "DashGlowL", new Vector3(-0.55f, 0.47f, 1.38f), new Vector3(0.7f, 0.02f, 0.25f), glowCyan);
            Part(PrimitiveType.Cube, "DashGlowR", new Vector3(0.55f, 0.47f, 1.38f), new Vector3(0.7f, 0.02f, 0.25f), glowAmber);

            // Crew. Front: pilot (left) and co-pilot (right). Rear: surgeon (left) and navigator (right).
            pilot = Crew("Pilot", new Color(0.15f, 0.3f, 0.75f), new Vector3(-0.55f, 0f, 0.8f), 0f);
            coPilot = Crew("Co-pilot", new Color(0.85f, 0.42f, 0.1f), new Vector3(0.55f, 0f, 0.8f), 0f);
            surgeon = Crew("Surgeon", new Color(0.15f, 0.55f, 0.35f), new Vector3(-1.05f, 0f, 0.15f), -90f);
            navigator = Crew("Navigator", new Color(0.5f, 0.25f, 0.6f), new Vector3(1.05f, 0f, 0.15f), 90f);

            // Pilot's control stick.
            stick = new GameObject("Stick").transform;
            stick.SetParent(transform, false);
            stick.localPosition = new Vector3(-0.55f, 0.42f, 1.18f);
            Transform shaft = Part(PrimitiveType.Cylinder, "Shaft", Vector3.zero, new Vector3(0.05f, 0.18f, 0.05f), trim);
            shaft.SetParent(stick, false);
            shaft.localPosition = new Vector3(0f, 0.18f, 0f);
            Transform grip = Part(PrimitiveType.Sphere, "Grip", Vector3.zero, Vector3.one * 0.09f, glowAmber);
            grip.SetParent(stick, false);
            grip.localPosition = new Vector3(0f, 0.38f, 0f);

            // Side consoles with live readouts.
            Part(PrimitiveType.Cube, "BeamConsole", new Vector3(-1.7f, 0.8f, 0.15f), new Vector3(0.4f, 0.7f, 1f), trim);
            beamScreen = Part(PrimitiveType.Cube, "BeamScreen", new Vector3(-1.49f, 1.1f, 0.15f), new Vector3(0.02f, 0.45f, 0.75f), screenGlow).GetComponent<Renderer>();
            Part(PrimitiveType.Cube, "VitalsConsole", new Vector3(1.7f, 0.8f, 0.15f), new Vector3(0.4f, 0.7f, 1f), trim);
            Part(PrimitiveType.Cube, "VitalsScreen", new Vector3(1.49f, 1.1f, 0.15f), new Vector3(0.02f, 0.45f, 0.75f), screenGlow);

            // Text faces into the cabin, readable from the camera.
            beamText = Label("BeamText", new Vector3(-1.47f, 1.1f, 0.15f), Quaternion.Euler(0f, -90f, 0f), 0.016f, new Color(0.7f, 0.95f, 1f));
            vitalsText = Label("VitalsText", new Vector3(1.47f, 1.1f, 0.15f), Quaternion.Euler(0f, 90f, 0f), 0.016f, new Color(0.6f, 1f, 0.8f));
            gunnerText = Label("GunnerText", new Vector3(0.55f, 0.49f, 1.25f), Quaternion.Euler(60f, 0f, 0f), 0.018f, new Color(1f, 0.75f, 0.4f));

            // Name tags.
            Label("PilotTag", new Vector3(-0.55f, 1.85f, 0.8f), Quaternion.identity, 0.016f, Color.white).text = "PILOT";
            Label("CoPilotTag", new Vector3(0.55f, 1.85f, 0.8f), Quaternion.identity, 0.016f, Color.white).text = "CO-PILOT";
            Label("SurgeonTag", new Vector3(-1.05f, 1.75f, 0.15f), Quaternion.identity, 0.012f, Color.white).text = "SURGEON";
            Label("NavigatorTag", new Vector3(1.05f, 1.75f, 0.15f), Quaternion.identity, 0.012f, Color.white).text = "NAVIGATOR";

            // Lights: warm cabin light, and a red alarm that flashes when something is wrong.
            AddLight("CabinLight", new Vector3(0f, 2.1f, 0f), new Color(1f, 0.9f, 0.8f), 1.1f, 6f);
            AddLight("DashLight", new Vector3(0f, 1f, 1.7f), new Color(0.4f, 0.8f, 1f), 0.8f, 3f);
            alarm = AddLight("Alarm", new Vector3(0f, 2.2f, 1.2f), new Color(1f, 0.1f, 0.05f), 0f, 7f);

            // Camera: behind and above the crew, looking out of the windshield.
            CameraMount = new GameObject("CockpitCameraMount").transform;
            CameraMount.SetParent(transform, false);
            // Just behind the front seats, at head height: the windshield fills most of the frame,
            // with the pilot and co-pilot below it and the surgeon and navigator at the sides.
            CameraMount.localPosition = new Vector3(0f, 1.75f, -1.1f);
            CameraMount.localRotation = Quaternion.LookRotation(new Vector3(0f, 1.5f, 1.75f) - CameraMount.localPosition);
        }

        private void Update()
        {
            if (ship == null)
            {
                return;
            }

            float t = Time.time;
            Vector3 localSpin = ship.InverseTransformDirection(shipBody != null ? shipBody.angularVelocity : Vector3.zero);
            Vector3 localVelocity = ship.InverseTransformDirection(shipBody != null ? shipBody.velocity : Vector3.zero);

            // Pilot: the stick follows the turn; the body leans into it.
            float yaw = Mathf.Clamp(localSpin.y * 25f, -25f, 25f);
            float pitch = Mathf.Clamp(-localSpin.x * 25f, -25f, 25f);
            stick.localRotation = Quaternion.Euler(-pitch, 0f, -yaw);
            pilot.torso.localRotation = Quaternion.Euler(Mathf.Clamp(localVelocity.z * 0.15f, -4f, 6f), 0f, -yaw * 0.25f);
            pilot.leftArm.localRotation = Quaternion.Euler(-pitch * 0.4f, 0f, yaw * 0.3f);
            pilot.rightArm.localRotation = Quaternion.Euler(-pitch * 0.4f, 0f, yaw * 0.3f);
            pilot.head.localRotation = Quaternion.Euler(0f, yaw * 0.5f, 0f);

            // Co-pilot: tracks the target; recoils with each shot.
            bool firing = (ShipInput.Read().Fire || ShipInput.Read().FireMedicine)
                || (shipCoPilot != null && shipCoPilot.CurrentTarget != null);
            if (firing && Mathf.Repeat(t, 0.25f) < Time.deltaTime)
            {
                coPilotRecoil = 1f;
            }
            coPilotRecoil = Mathf.MoveTowards(coPilotRecoil, 0f, Time.deltaTime * 6f);
            float look = 0f;
            if (shipCoPilot != null && shipCoPilot.CurrentTarget != null)
            {
                Vector3 to = ship.InverseTransformPoint(shipCoPilot.CurrentTarget.transform.position);
                look = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -60f, 60f);
            }
            coPilot.head.localRotation = Quaternion.Slerp(coPilot.head.localRotation, Quaternion.Euler(0f, look, 0f), Time.deltaTime * 6f);
            coPilot.rightArm.localRotation = Quaternion.Euler(-10f - coPilotRecoil * 15f, 0f, 0f);
            coPilot.leftArm.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            coPilot.torso.localRotation = Quaternion.Euler(-coPilotRecoil * 4f, look * 0.2f, 0f);

            // Surgeon: leans over the beam console while the beam is in use.
            bool beaming = beam != null && beam.IsActive;
            float reach = beaming ? 1f : 0f;
            surgeon.torso.localRotation = Quaternion.Slerp(surgeon.torso.localRotation, Quaternion.Euler(reach * 18f, 0f, 0f), Time.deltaTime * 5f);
            float work = beaming ? Mathf.Sin(t * 9f) * 8f : 0f;
            surgeon.leftArm.localRotation = Quaternion.Euler(-35f * reach + work, 0f, 0f);
            surgeon.rightArm.localRotation = Quaternion.Euler(-35f * reach - work, 0f, 0f);
            if (beamScreen != null)
            {
                Color tool = beam != null ? SurgicalBeam.ToolColor(beam.CurrentTool) : Color.cyan;
                beamScreen.material.SetColor("_EmissionColor", tool * (beaming ? 0.35f + 0.1f * Mathf.Sin(t * 20f) : 0.08f));
            }

            // Navigator: mostly on the vitals screen, glancing up at the windshield.
            float glance = Mathf.PerlinNoise(t * 0.3f, 4.2f) > 0.6f ? -45f : 0f;
            navigator.head.localRotation = Quaternion.Slerp(navigator.head.localRotation, Quaternion.Euler(10f, glance, 0f), Time.deltaTime * 3f);
            navigator.rightArm.localRotation = Quaternion.Euler(-30f + Mathf.Sin(t * 2f) * 4f, 0f, 0f);
            navigator.leftArm.localRotation = Quaternion.Euler(-30f, 0f, 0f);

            // Breathing, so nobody looks frozen.
            float breathe = 1f + Mathf.Sin(t * 1.6f) * 0.015f;
            foreach (CrewMember c in new[] { pilot, coPilot, surgeon, navigator })
            {
                c.torso.localScale = new Vector3(1f, breathe, 1f);
            }

            // Alarm: red flash on heavy blood loss, amber blink while the pilot is holding a wall.
            GameManager gm = GameManager.Instance;
            bool bleeding = gm != null && gm.BloodLoss01 > 0.5f;
            bool wall = shipPilot != null && shipPilot.IsHoldingWall;
            alarm.color = bleeding ? new Color(1f, 0.1f, 0.05f) : new Color(1f, 0.55f, 0.1f);
            alarm.intensity = bleeding ? 2.5f * Mathf.Abs(Mathf.Sin(t * 4f)) : wall ? 1.5f : 0f;

            if (Time.unscaledTime >= nextTextUpdate)
            {
                nextTextUpdate = Time.unscaledTime + 0.25f;
                UpdateReadouts(gm);
            }
        }

        private void UpdateReadouts(GameManager gm)
        {
            if (gm != null)
            {
                vitalsText.text = $"VITALS\nBLOOD LOSS {gm.BloodLoss01 * 100f:F0}%\nTREATED {gm.CellsTreated}/{gm.RequiredTreatments}\nDAMAGE LEFT {ArteryLesion.Active.Count}";
            }

            if (beam != null)
            {
                string state = !beam.IsActive ? "STANDBY" : beam.Target == null ? "NO LOCK" : beam.Target.Title;
                beamText.text = $"SURGICAL BEAM\n{beam.CurrentToolNumber} {SurgicalBeam.ToolName(beam.CurrentTool).ToUpper()}\n{state}";
            }

            if (shipCoPilot != null)
            {
                gunnerText.text = shipCoPilot.CurrentMode == ShipCoPilot.Mode.Off
                    ? "GUNS: YOURS"
                    : shipCoPilot.CurrentTarget == null ? "GUNS: SCANNING"
                    : shipCoPilot.CurrentTarget.IsCurable ? "TARGET: INFECTED" : "TARGET: MALIGNANT";
            }
        }

        // ─── Building helpers ───────────────────────────────────────────────────────

        // A seated figure: seat, legs, torso, helmeted head and two arms that pivot at the shoulder.
        private CrewMember Crew(string name, Color suit, Vector3 position, float yaw)
        {
            Material suitMat = Mat(suit, 0.3f, Color.black);
            Material helmetMat = Mat(new Color(0.9f, 0.9f, 0.92f), 0.7f, Color.black);
            Material visorMat = Mat(new Color(0.05f, 0.08f, 0.12f), 0.95f, new Color(0.02f, 0.05f, 0.08f), 0.5f);
            Material seatMat = Mat(new Color(0.2f, 0.2f, 0.22f), 0.3f, Color.black);

            Transform root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Child(root, PrimitiveType.Cube, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.1f, 0.55f), seatMat);
            Child(root, PrimitiveType.Cube, "SeatBack", new Vector3(0f, 0.8f, -0.3f), new Vector3(0.55f, 0.65f, 0.08f), seatMat);
            Transform legL = Child(root, PrimitiveType.Capsule, "LegL", new Vector3(-0.12f, 0.55f, 0.22f), new Vector3(0.14f, 0.25f, 0.14f), suitMat);
            legL.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Transform legR = Child(root, PrimitiveType.Capsule, "LegR", new Vector3(0.12f, 0.55f, 0.22f), new Vector3(0.14f, 0.25f, 0.14f), suitMat);
            legR.localRotation = Quaternion.Euler(90f, 0f, 0f);

            CrewMember c = new CrewMember();
            c.torso = new GameObject("TorsoPivot").transform;
            c.torso.SetParent(root, false);
            c.torso.localPosition = new Vector3(0f, 0.55f, -0.05f);
            Child(c.torso, PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.4f, 0f), new Vector3(0.42f, 0.32f, 0.3f), suitMat);

            c.head = new GameObject("HeadPivot").transform;
            c.head.SetParent(c.torso, false);
            c.head.localPosition = new Vector3(0f, 0.85f, 0f);
            Child(c.head, PrimitiveType.Sphere, "Helmet", new Vector3(0f, 0.08f, 0f), Vector3.one * 0.3f, helmetMat);
            Child(c.head, PrimitiveType.Sphere, "Visor", new Vector3(0f, 0.08f, 0.1f), new Vector3(0.24f, 0.13f, 0.14f), visorMat);

            c.leftArm = Arm(c.torso, new Vector3(-0.25f, 0.65f, 0f), suitMat);
            c.rightArm = Arm(c.torso, new Vector3(0.25f, 0.65f, 0f), suitMat);
            return c;
        }

        private Transform Arm(Transform torso, Vector3 shoulder, Material material)
        {
            Transform pivot = new GameObject("ArmPivot").transform;
            pivot.SetParent(torso, false);
            pivot.localPosition = shoulder;
            // Upper and forearm in one, reaching forward and down toward the controls.
            Transform arm = Child(pivot, PrimitiveType.Capsule, "Arm", new Vector3(0f, -0.12f, 0.2f), new Vector3(0.1f, 0.26f, 0.1f), material);
            arm.localRotation = Quaternion.Euler(60f, 0f, 0f);
            return pivot;
        }

        private Transform Part(PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            return Child(transform, type, name, localPosition, scale, material);
        }

        private static Transform Child(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Strip(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        // The set needs no physics.
        private static void Strip(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Destroy(c);
            }
        }

        private static void FlipInsideOut(MeshFilter filter)
        {
            Mesh mesh = Instantiate(filter.sharedMesh);
            int[] tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                (tris[i], tris[i + 1]) = (tris[i + 1], tris[i]);
            }
            mesh.triangles = tris;
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = -normals[i];
            }
            mesh.normals = normals;
            filter.sharedMesh = mesh;
        }

        private TextMesh Label(string name, Vector3 localPosition, Quaternion rotation, float size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation;
            TextMesh text = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.fontSize = 48;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            return text;
        }

        private Light AddLight(string name, Vector3 localPosition, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Material Mat(Color color, float gloss, Color emission, float metallic = 0f)
        {
            Material m = new Material(Shader.Find("Standard"));
            m.color = color;
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metallic);
            if (emission != Color.black)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        // Shows the nose camera's view at full brightness regardless of the cabin lighting.
        private static Material ScreenMaterial(Texture view)
        {
            Shader unlit = Shader.Find("Unlit/Texture");
            if (unlit != null)
            {
                return new Material(unlit) { mainTexture = view };
            }

            Material m = new Material(Shader.Find("Standard")) { color = Color.black };
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", view);
            m.SetColor("_EmissionColor", Color.white);
            return m;
        }
    }
}
