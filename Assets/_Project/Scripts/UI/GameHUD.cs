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

            GUI.color = Color.white;
        }
    }
}
