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

            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(20, _virtualHeight - 64, 1800, 30),
                "Cells - green INFECTED: medicine (right-click).   Spiky purple MALIGNANT: destroy (left-click).", _hintStyle);
            GUI.Label(new Rect(20, _virtualHeight - 40, 1800, 30),
                "Artery - PLAQUE: laser (left-click).  CLOT: medicine (right-click).  Beam (pick 1-4, hold F): 1 TEAR suture, 2 ANEURYSM stent, 3 BLOCKAGE balloon, 4 FOREIGN OBJECT grabber.", _hintStyle);
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

            float along = nearest.transform.position.z - ship.transform.position.z;
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
                    : "wrong tool for " + beam.Target.Title.ToLower() + " - " + beam.Target.ToolHint;
                GUI.color = SurgicalBeam.ToolColor(beam.CurrentTool);
                GUI.Label(new Rect(20, 330, 1100, 60), $"Surgical beam [1-4]: {tool} - {state}", _labelStyle);
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
                ? "MISSION COMPLETE - the artery is clear"
                : gameMan.HealthyCellsLost >= gameMan.MaxHealthyCellsLost
                    ? "MISSION FAILED - too many curable cells destroyed"
                    : gameMan.BloodLoss01 >= 1f
                        ? "MISSION FAILED - the patient lost too much blood"
                        : "MISSION FAILED";
            GUI.Label(new Rect(0, _virtualHeight * 0.4f, _virtualWidth, 80), text, _bannerStyle);
        }
    }
}
