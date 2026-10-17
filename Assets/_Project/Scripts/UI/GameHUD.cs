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

            GUI.color = Color.cyan;
            GUI.Label(new Rect(20, 20, 400, 60), $"Health: {shipHealth.CurrentHealth:F1} / {shipHealth.MaxHealth:F1}", _labelStyle);
            GUI.Label(new Rect(20, 60, 400, 60), $"Time: {gameMan.RemainingTime:F1}s", _labelStyle);

            DrawMission(gameMan);
            DrawCrew(shipHealth.gameObject);
            DrawEndBanner(gameMan);

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

            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(20, Screen.height - 40, 1200, 30),
                "Green INFECTED cells: medicine (right-click).   Spiky purple MALIGNANT cells: destroy (left-click).", _hintStyle);
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
                    : "MISSION FAILED";
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 80), text, _bannerStyle);
        }
    }
}
