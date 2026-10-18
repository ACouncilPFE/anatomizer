using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Puts the crew (ShipPilot and ShipCoPilot) the surgical beam, the ship's sounds and the cockpit window aboard every ship when a scene
    /// loads, so scenes built before they existed get them without being rebuilt or edited.
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
                if (ship.GetComponent<SurgicalBeam>() == null)
                {
                    ship.gameObject.AddComponent<SurgicalBeam>();
                }
                if (ship.GetComponent<ShipAudio>() == null)
                {
                    ship.gameObject.AddComponent<ShipAudio>();
                }
                if (ship.GetComponent<CockpitView>() == null)
                {
                    ship.gameObject.AddComponent<CockpitView>();
                }
            }
        }
    }
}
