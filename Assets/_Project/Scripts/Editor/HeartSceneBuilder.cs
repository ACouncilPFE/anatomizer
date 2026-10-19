using AIGame.Core;
using AIGame.Gameplay;
using AIGame.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIGame.Editor
{
    /// <summary>
    /// Builds the mission scenes that create their environment at load: Mission 2 (the heart) and
    /// Mission 3 (the gut). Each gets the ship at the start of its first passage, the usual camera,
    /// HUD, game manager and spawner, plus its environment object. Also lists every mission scene
    /// in the build settings so F1-F3 can switch between them. Uses the prefabs and spawn profile
    /// made by AIGame > Build Prototype Scene.
    /// </summary>
    public static class HeartSceneBuilder
    {
        public const string ArteryScenePath = "Assets/_Project/Scenes/Mission_01_Artery.unity";
        public const string ScenePath = "Assets/_Project/Scenes/Mission_02_Heart.unity";
        public const string GutScenePath = "Assets/_Project/Scenes/Mission_03_Gut.unity";

        [MenuItem("AIGame/Build Heart Scene (Mission 2)")]
        public static void BuildFromMenu()
        {
            if (Confirm(ScenePath))
            {
                Build();
            }
        }

        [MenuItem("AIGame/Build Gut Scene (Mission 3)")]
        public static void BuildGutFromMenu()
        {
            if (Confirm(GutScenePath))
            {
                BuildGut();
            }
        }

        /// <summary>For batch mode: -executeMethod AIGame.Editor.HeartSceneBuilder.BuildForBatch</summary>
        public static void BuildForBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        /// <summary>For batch mode: -executeMethod AIGame.Editor.HeartSceneBuilder.BuildGutForBatch</summary>
        public static void BuildGutForBatch()
        {
            BuildGut();
            EditorApplication.Exit(0);
        }

        public static void Build()
        {
            BuildMission(ScenePath, 8, 1500f, root =>
            {
                root.name = "Heart";
                root.AddComponent<HeartBeat>();
                root.AddComponent<HeartEnvironment>();
            });
        }

        public static void BuildGut()
        {
            BuildMission(GutScenePath, 8, 1500f, root =>
            {
                root.name = "Gut";
                root.AddComponent<GutMotility>();
                root.AddComponent<GutEnvironment>();
            });
        }

        private static bool Confirm(string path)
        {
            return EditorUtility.DisplayDialog("Build Mission Scene",
                "This will create (or replace) the scene at:\n" + path + "\n\nProceed?", "Build", "Cancel");
        }

        private static void BuildMission(string path, int requiredTreatments, float duration, System.Action<GameObject> addEnvironment)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PrototypeSceneBuilder.CreateDirectionalLight();
            GameObject camObj = PrototypeSceneBuilder.CreateCamera();
            GameObject ship = PrototypeSceneBuilder.CreateShip();

            SerializedObject follow = new SerializedObject(camObj.GetComponent<CameraFollow>());
            follow.FindProperty("target").objectReferenceValue = ship.transform;
            follow.ApplyModifiedPropertiesWithoutUndo();

            PrototypeSceneBuilder.CreateGameManager();
            GameManager gm = Object.FindObjectOfType<GameManager>();
            SerializedObject gmSO = new SerializedObject(gm);
            gmSO.FindProperty("requiredTreatments").intValue = requiredTreatments;
            gmSO.FindProperty("missionDurationSeconds").floatValue = duration;
            gmSO.ApplyModifiedPropertiesWithoutUndo();

            GameObject spawnerObj = PrototypeSceneBuilder.CreateSpawner(ship);
            WireSpawner(spawnerObj);
            PrototypeSceneBuilder.CreateHUD();
            PrototypeSceneBuilder.CreateObjectiveManager();

            addEnvironment(new GameObject("Environment"));

            Projectile projectile = AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Project/Prefabs/Projectile.prefab");
            if (projectile != null)
            {
                SerializedObject weapon = new SerializedObject(ship.GetComponent<WeaponSystem>());
                weapon.FindProperty("projectilePrefab").objectReferenceValue = projectile;
                weapon.ApplyModifiedPropertiesWithoutUndo();
            }

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, path);
            AddMissionsToBuildSettings();
            Debug.Log("[HeartSceneBuilder] Mission scene saved to " + path);
        }

        private static void WireSpawner(GameObject spawnerObj)
        {
            MissionZoneSpawnProfile profile = AssetDatabase.LoadAssetAtPath<MissionZoneSpawnProfile>("Assets/_Project/ScriptableObjects/SpawnProfile_Artery.asset");
            MissionZoneSpawner spawner = spawnerObj.GetComponent<MissionZoneSpawner>();
            if (profile == null || spawner == null)
            {
                Debug.LogWarning("[HeartSceneBuilder] No spawn profile found; run AIGame > Build Prototype Scene once first.");
                return;
            }

            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty bindings = so.FindProperty("zoneBindings");
            bindings.arraySize = 1;
            SerializedProperty binding = bindings.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("profile").objectReferenceValue = profile;
            SerializedProperty points = binding.FindPropertyRelative("spawnPoints");
            points.arraySize = spawnerObj.transform.childCount;
            for (int i = 0; i < points.arraySize; i++)
            {
                points.GetArrayElementAtIndex(i).objectReferenceValue = spawnerObj.transform.GetChild(i);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void AddMissionsToBuildSettings()
        {
            System.Collections.Generic.List<EditorBuildSettingsScene> scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (string p in new[] { ArteryScenePath, ScenePath, GutScenePath })
            {
                if (System.IO.File.Exists(p))
                {
                    scenes.Add(new EditorBuildSettingsScene(p, true));
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
