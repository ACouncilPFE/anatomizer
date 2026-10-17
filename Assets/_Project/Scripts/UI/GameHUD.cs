using AIGame.Core;
using AIGame.Gameplay;
using UnityEngine;

namespace AIGame.UI
{
    public class GameHUD : MonoBehaviour
    {
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;

        private void OnGUI()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                _boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 18 };
            }

            ShipHealth shipHealth = FindObjectOfType<ShipHealth>();
            GameManager gameMan = GameManager.Instance;

            if (shipHealth == null || gameMan == null)
                return;

            GUI.color = Color.cyan;
            GUI.Label(new Rect(20, 20, 400, 60), $"Health: {shipHealth.CurrentHealth:F1} / {shipHealth.MaxHealth:F1}", _labelStyle);
            GUI.Label(new Rect(20, 60, 400, 60), $"Time: {gameMan.RemainingTime:F1}s", _labelStyle);

            DrawCrew(shipHealth.gameObject);

            GUI.color = Color.white;
        }

        private void DrawCrew(GameObject ship)
        {
            ShipPilot pilot = ship.GetComponent<ShipPilot>();
            if (pilot != null)
            {
                string pilotMode = pilot.CurrentMode == ShipPilot.Mode.Autopilot ? "Autopilot" : "Assist (you fly)";
                string wall = pilot.IsHoldingWall ? " - holding off the wall" : "";
                GUI.color = pilot.IsHoldingWall ? Color.yellow : Color.cyan;
                GUI.Label(new Rect(20, 100, 700, 60), $"Pilot [P]: {pilotMode}{wall}", _labelStyle);
            }

            ShipCoPilot coPilot = ship.GetComponent<ShipCoPilot>();
            if (coPilot != null)
            {
                ShipCoPilot.Mode coMode = coPilot.CurrentMode;
                string status = coMode == ShipCoPilot.Mode.Off
                    ? "Off (holding fire)"
                    : $"{coMode} - {(coPilot.CurrentTarget != null ? "engaging " + coPilot.CurrentTarget.Archetype : "scanning")}";
                GUI.color = coMode == ShipCoPilot.Mode.Repair ? Color.green
                          : coMode == ShipCoPilot.Mode.Destroy ? new Color(1f, 0.55f, 0.3f)
                          : Color.gray;
                GUI.Label(new Rect(20, 140, 900, 60),
                    $"Co-pilot [R]: {status}   Destroyed {coPilot.DestroyedCount}  Repaired {coPilot.RepairedCount}",
                    _labelStyle);
            }
        }
    }
}
