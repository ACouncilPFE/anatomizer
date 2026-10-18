using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Switches between missions until the body map exists: F1 for Mission 1 (the artery),
    /// F2 for Mission 2 (the heart). Each mission is its own scene, listed in the build settings.
    /// </summary>
    public static class MissionSelector
    {
        public static readonly (KeyCode key, string scene, string title)[] Missions =
        {
            (KeyCode.F1, "Mission_01_Artery", "Mission 1: Arterial Descent"),
            (KeyCode.F2, "Mission_02_Heart", "Mission 2: Cardiac Maelstrom"),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindObjectOfType<Listener>() == null)
            {
                GameObject go = new GameObject("MissionSelector");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Listener>();
            }
        }

        public static void Load(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(scene);
            }
            else
            {
                Debug.LogWarning("[MissionSelector] Scene not in the build settings: " + scene);
            }
        }

        private class Listener : MonoBehaviour
        {
            private void Update()
            {
                foreach (var mission in Missions)
                {
                    if (Input.GetKeyDown(mission.key))
                    {
                        Load(mission.scene);
                    }
                }
            }
        }
    }
}
