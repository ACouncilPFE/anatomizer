using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Puts the crew (ShipPilot and ShipCoPilot) aboard every ship when a scene loads, so scenes
    /// built before the crew existed get one without being rebuilt or edited.
    /// </summary>
    public static class ShipCrew
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (scene, mode) => Board();
            Board();
        }

        public static void Board()
        {
            foreach (ShipController ship in Object.FindObjectsOfType<ShipController>())
            {
                if (ship.GetComponent<ShipPilot>() == null)
                {
                    ship.gameObject.AddComponent<ShipPilot>();
                }
                if (ship.GetComponent<ShipCoPilot>() == null)
                {
                    ship.gameObject.AddComponent<ShipCoPilot>();
                }
            }
        }
    }
}
