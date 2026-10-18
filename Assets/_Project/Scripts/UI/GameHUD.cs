using AIGame.Core;
using AIGame.Gameplay;
using UnityEngine;

namespace AIGame.UI
{
    public class GameHUD : MonoBehaviour
    {
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _bannerStyle;
        private float _virtualWidth;
        private float _virtualHeight;

        private void OnGUI()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                _boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 18 };
                _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            }

            ShipHealth shipHealth = FindObjectOfType<ShipHealth>();
            GameManager gameMan = GameManager.Instance;

            if (shipHealth == null || gameMan == null)
                return;

            // Lay the HUD out for a 1080-pixel-tall screen and scale it to the real one, so it
            // fits a small Game view without the lines running into each other.
            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.35f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _virtualWidth = Screen.width / scale;
            _virtualHeight = Screen.height / scale;

            GUI.color = Color.cyan;
            GUI.Label(new Rect(20, 20, 400, 60), $"Health: {shipHealth.CurrentHealth:F1} / {shipHealth.MaxHealth:F1}", _labelStyle);
            GUI.Label(new Rect(20, 60, 400, 60), $"Time: {gameMan.RemainingTime:F1}s", _labelStyle);

            DrawMission(gameMan);
            DrawCrew(shipHealth.gameObject);
            DrawEndBanner(gameMan);

            GUI.color = Color.white;
            GUI.matrix = previousMatrix;

            DrawCrosshair(shipHealth.transform);
        }

        // Where the guns and beam point: a small cross ahead of the nose.
        private void DrawCrosshair(Transform ship)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(ship.position + ship.forward * 30f);
            if (screen.z <= 0f)
            {
                return;
            }

            float x = screen.x;
            float y = Screen.height - screen.y;
            GUI.color = new Color(0.7f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(x - 12f, y - 1f, 9f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + 3f, y - 1f, 9f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1f, y - 12f, 2f, 9f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1f, y + 3f, 2f, 9f), Texture2D.whiteTexture);

            // The selected beam tool, right by the crosshair, so F never surprises you.
            SurgicalBeam beam = ship.GetComponent<SurgicalBeam>();
            if (beam != null)
            {
                GUI.color = SurgicalBeam.ToolColor(beam.CurrentTool);
                GUI.Label(new Rect(x + 14f, y + 6f, 220f, 20f), $"F: {beam.CurrentToolNumber} {SurgicalBeam.ToolName(beam.CurrentTool)}");
            }
            GUI.color = Color.white;
        }

        private void DrawMission(GameManager gameMan)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 100, 900, 60),
                $"Cells treated: {gameMan.CellsTreated} / {gameMan.RequiredTreatments}", _labelStyle);

            bool danger = gameMan.HealthyCellsLost >= gameMan.MaxHealthyCellsLost - 1;
            GUI.color = danger ? Color.red : new Color(1f, 0.75f, 0.75f);
            GUI.Label(new Rect(20, 130, 900, 60),
                $"Curable cells destroyed: {gameMan.HealthyCellsLost} / {gameMan.MaxHealthyCellsLost}", _labelStyle);

            float blood = gameMan.BloodLoss01;
            GUI.color = blood > 0.6f ? Color.red : blood > 0.3f ? Color.yellow : new Color(1f, 0.75f, 0.75f);
            GUI.Label(new Rect(20, 250, 900, 60), $"Patient blood loss: {blood * 100f:F0}%", _labelStyle);

            DrawNearestLesion();
            DrawHeart(gameMan);

            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            string missions = "";
            foreach (var mission in MissionSelector.Missions)
            {
                missions += $"[{mission.key}] {mission.title}    ";
            }
            GUI.Label(new Rect(20, _virtualHeight - 88, 1800, 30), missions, _hintStyle);

            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(20, _virtualHeight - 64, 1800, 30),
                "Cells - green INFECTED: medicine (right-click).   Spiky purple MALIGNANT: destroy (left-click).", _hintStyle);
            GUI.Label(new Rect(20, _virtualHeight - 40, 1800, 30),
                "[V] cockpit view.   Artery - PLAQUE: laser (left-click).  CLOT: medicine (right-click).  Beam (pick 1-4, hold F): 1 TEAR suture, 2 ANEURYSM stent, 3 BLOCKAGE balloon, 4 FOREIGN OBJECT grabber.", _hintStyle);
        }

        // Mission 2: heart-rhythm trace, valve states, heart strain and surge warnings.
        private void DrawHeart(GameManager gameMan)
        {
            HeartBeat heart = HeartBeat.Instance;
            if (heart == null)
            {
                return;
            }

            // ECG trace, top right: the last three seconds, scrolling.
            const float width = 420f;
            const float height = 90f;
            float x0 = _virtualWidth - width - 20f;
            float y0 = 120f;
            GUI.color = new Color(0f, 0.05f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(x0 - 4f, y0 - 24f, width + 8f, height + 30f), Texture2D.whiteTexture);
            GUI.color = heart.Arrhythmic ? new Color(1f, 0.5f, 0.3f) : new Color(0.4f, 1f, 0.5f);
            float bpm = 60f / Mathf.Max(heart.Interval, 0.1f);
            GUI.Label(new Rect(x0, y0 - 24f, width, 24f),
                heart.Arrhythmic ? $"ECG  {bpm:F0} bpm  ARRHYTHMIA - ablate the scar tissue" : $"ECG  {bpm:F0} bpm  steady", _hintStyle);

            const int points = 140;
            float now = Time.time;
            for (int i = 0; i < points; i++)
            {
                float t = now - 3f + 3f * i / (points - 1);
                float v = heart.EcgAt(t);
                float px = x0 + width * i / (points - 1);
                float py = y0 + height * 0.7f - v * height * 0.55f;
                GUI.DrawTexture(new Rect(px, py, 3f, 3f), Texture2D.whiteTexture);
            }

            // Valves.
            HeartEnvironment env = FindObjectOfType<HeartEnvironment>();
            if (env != null && env.Tricuspid != null && env.Pulmonary != null)
            {
                DrawValve(env.Tricuspid, x0, y0 + height + 12f);
                DrawValve(env.Pulmonary, x0 + width * 0.5f, y0 + height + 12f);

                if (env.PressureSurgeAtShip())
                {
                    GUI.color = new Color(1f, 0.3f, 0.2f, 0.6f + 0.4f * Mathf.Sin(now * 20f));
                    GUI.Label(new Rect(x0, y0 + height + 40f, width, 30f), "PRESSURE SURGE - hold on", _labelStyle);
                }
            }

            // Heart strain.
            float strain = gameMan.Strain01;
            GUI.color = strain > 0.6f ? Color.red : strain > 0.3f ? Color.yellow : new Color(1f, 0.75f, 0.85f);
            GUI.Label(new Rect(20, 370, 900, 60), $"Heart strain: {strain * 100f:F0}%", _labelStyle);
        }

        private void DrawValve(HeartValve valve, float x, float y)
        {
            GUI.color = valve.IsOpen ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.45f, 0.4f);
            GUI.Label(new Rect(x, y, 220f, 26f), $"{valve.DisplayName}: {(valve.IsOpen ? "OPEN" : "SHUT")}", _hintStyle);
        }

        // Points you at the closest wall damage still to be fixed.
        private void DrawNearestLesion()
        {
            ShipController ship = FindObjectOfType<ShipController>();
            if (ship == null)
            {
                return;
            }

            ArteryLesion nearest = null;
            float best = float.MaxValue;
            foreach (ArteryLesion lesion in ArteryLesion.Active)
            {
                float d = Vector3.Distance(ship.transform.position, lesion.transform.position);
                if (d < best)
                {
                    best = d;
                    nearest = lesion;
                }
            }

            if (nearest == null)
            {
                GUI.color = new Color(0.5f, 1f, 0.6f);
                GUI.Label(new Rect(20, 290, 900, 60), "Artery: all damage repaired", _labelStyle);
                return;
            }

            float along = Vector3.Dot(nearest.transform.position - ship.transform.position, ship.transform.forward);
            string where = Mathf.Abs(along) < 3f ? "right here" : along > 0f ? $"{along:F0} m ahead" : $"{-along:F0} m behind";
            GUI.color = nearest.Type == LesionType.Tear ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.85f, 0.55f);
            GUI.Label(new Rect(20, 290, 900, 60),
                $"Artery damage left: {ArteryLesion.Active.Count}   Nearest: {nearest.Title}, {where} - {nearest.ToolHint}", _labelStyle);

            SurgicalBeam beam = ship.GetComponent<SurgicalBeam>();
            if (beam != null)
            {
                string tool = $"{beam.CurrentToolNumber} {SurgicalBeam.ToolName(beam.CurrentTool)}";
                string state = !beam.IsActive ? "hold F to use"
                    : beam.Target == null ? "point the nose at a problem (within 22 m)"
                    : beam.Target.RequiredTool == beam.CurrentTool ? "working on " + beam.Target.Title.ToLower() + "..."
                    : $"WRONG TOOL for {beam.Target.Title.ToLower()} - {beam.Target.ToolHint}";
                GUI.color = SurgicalBeam.ToolColor(beam.CurrentTool);
                GUI.Label(new Rect(20, 330, 1100, 60), $"Beam tool: {tool} (press 1-4 to change) - {state}", _labelStyle);
            }
        }

        private void DrawCrew(GameObject ship)
        {
            ShipPilot pilot = ship.GetComponent<ShipPilot>();
            if (pilot != null)
            {
                string pilotMode = pilot.CurrentMode == ShipPilot.Mode.Autopilot ? "Autopilot" : "Assist (you fly)";
                string wall = pilot.IsHoldingWall ? " - holding off the wall" : "";
                GUI.color = pilot.IsHoldingWall ? Color.yellow : Color.cyan;
                GUI.Label(new Rect(20, 170, 700, 60), $"Pilot [P]: {pilotMode}{wall}", _labelStyle);
            }

            ShipCoPilot coPilot = ship.GetComponent<ShipCoPilot>();
            if (coPilot != null)
            {
                bool on = coPilot.CurrentMode == ShipCoPilot.Mode.Treat;
                string status = !on
                    ? "Off (holding fire)"
                    : coPilot.CurrentTarget == null
                        ? "Treating - scanning"
                        : coPilot.CurrentTarget.IsCurable ? "Treating - medicine on infected cell" : "Treating - destroying malignant cell";
                GUI.color = on ? new Color(0.6f, 1f, 0.5f) : Color.gray;
                GUI.Label(new Rect(20, 210, 900, 60), $"Co-pilot [R]: {status}", _labelStyle);
            }
        }

        private void DrawEndBanner(GameManager gameMan)
        {
            if (gameMan.CurrentState != GameState.Success && gameMan.CurrentState != GameState.Failure)
            {
                return;
            }

            if (_bannerStyle == null)
            {
                _bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }

            bool won = gameMan.CurrentState == GameState.Success;
            GUI.color = won ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            string text = won
                ? (HeartBeat.Instance != null ? "MISSION COMPLETE - the heart is beating strong" : "MISSION COMPLETE - the artery is clear")
                : gameMan.HealthyCellsLost >= gameMan.MaxHealthyCellsLost
                    ? "MISSION FAILED - too many curable cells destroyed"
                    : gameMan.BloodLoss01 >= 1f
                        ? "MISSION FAILED - the patient lost too much blood"
                    : gameMan.Strain01 >= 1f
                        ? "MISSION FAILED - the heart gave out"
                        : "MISSION FAILED";
            GUI.Label(new Rect(0, _virtualHeight * 0.4f, _virtualWidth, 80), text, _bannerStyle);
        }
    }
}
