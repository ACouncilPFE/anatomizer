using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Switches between missions until the body map exists. Press M for an on-screen mission menu
    /// (click a mission or press its number), or F1 / F2 / F3 directly. Ctrl+number is not used:
    /// in the Unity editor those keys open editor windows. Each mission is its own scene, listed in
    /// the build settings.
    /// </summary>
    public static class MissionSelector
    {
        public static readonly (KeyCode key, KeyCode number, string scene, string title)[] Missions =
        {
            (KeyCode.F1, KeyCode.Alpha1, "Mission_01_Artery", "Mission 1: Arterial Descent"),
            (KeyCode.F2, KeyCode.Alpha2, "Mission_02_Heart", "Mission 2: Cardiac Maelstrom"),
            (KeyCode.F3, KeyCode.Alpha3, "Mission_03_Gut", "Mission 3: Gut Run"),
        };

        /// <summary>True while the mission menu is showing.</summary>
        public static bool MenuOpen { get; private set; }

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
            MenuOpen = false;
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
            private GUIStyle titleStyle;
            private GUIStyle buttonStyle;
            private GUIStyle hintStyle;

            private void Update()
            {
                if (Input.GetKeyDown(KeyCode.M))
                {
                    MenuOpen = !MenuOpen;
                }

                foreach (var mission in Missions)
                {
                    if (Input.GetKeyDown(mission.key) || (MenuOpen && Input.GetKeyDown(mission.number)))
                    {
                        Load(mission.scene);
                    }
                }
            }

            private void OnGUI()
            {
                if (!MenuOpen)
                {
                    return;
                }

                GUI.depth = -10; // above the HUD
                titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 20 };
                hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };

                float w = 520f;
                float h = 110f + Missions.Length * 60f;
                Rect box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(box.x, box.y + 10f, w, 40f), "Choose a mission", titleStyle);

                string current = SceneManager.GetActiveScene().name;
                for (int i = 0; i < Missions.Length; i++)
                {
                    var mission = Missions[i];
                    Rect button = new Rect(box.x + 30f, box.y + 60f + i * 60f, w - 60f, 48f);
                    string label = $"{i + 1}   {mission.title}" + (mission.scene == current ? "   (current - restart)" : "");
                    if (GUI.Button(button, label, buttonStyle))
                    {
                        Load(mission.scene);
                    }
                }
                GUI.Label(new Rect(box.x, box.y + h - 40f, w, 30f), "Click a mission or press its number.  M to close.", hintStyle);
            }
        }
    }
}
