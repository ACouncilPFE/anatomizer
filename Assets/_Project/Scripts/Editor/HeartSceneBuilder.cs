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
    /// Builds the Mission 2 scene (the heart): the ship in the vena cava, the usual camera,
    /// HUD and game manager, and a HeartEnvironment that builds the heart at load. Also puts both
    /// mission scenes in the build settings so F1 / F2 can switch between them.
    /// Uses the prefabs and spawn profile made by AIGame > Build Prototype Scene.
    /// </summary>
    public static class HeartSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Mission_02_Heart.unity";
        public const string ArteryScenePath = "Assets/_Project/Scenes/Mission_01_Artery.unity";

        [MenuItem("AIGame/Build Heart Scene (Mission 2)")]
        public static void BuildFromMenu()
        {
            if (EditorUtility.DisplayDialog("Build Heart Scene",
                    "This will create (or replace) the Mission 2 scene at:\n" + ScenePath + "\n\nProceed?", "Build", "Cancel"))
            {
                Build();
                EditorUtility.DisplayDialog("Done", "Mission 2 scene created at:\n" + ScenePath, "OK");
            }
        }

        /// <summary>For batch mode: -executeMethod AIGame.Editor.HeartSceneBuilder.BuildForBatch</summary>
        public static void BuildForBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        public static void Build()
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
            gmSO.FindProperty("requiredTreatments").intValue = 8; // 5 heart problems + 3 cells
            gmSO.FindProperty("missionDurationSeconds").floatValue = 1500f;
            gmSO.ApplyModifiedPropertiesWithoutUndo();

            GameObject spawnerObj = PrototypeSceneBuilder.CreateSpawner(ship);
            WireSpawner(spawnerObj);
            PrototypeSceneBuilder.CreateHUD();
            PrototypeSceneBuilder.CreateObjectiveManager();

            GameObject heart = new GameObject("Heart");
            heart.AddComponent<HeartBeat>();
            heart.AddComponent<HeartEnvironment>();

            Projectile projectile = AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Project/Prefabs/Projectile.prefab");
            if (projectile != null)
            {
                SerializedObject weapon = new SerializedObject(ship.GetComponent<WeaponSystem>());
                weapon.FindProperty("projectilePrefab").objectReferenceValue = projectile;
                weapon.ApplyModifiedPropertiesWithoutUndo();
            }

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddMissionsToBuildSettings();
            Debug.Log("[HeartSceneBuilder] Mission 2 scene saved to " + ScenePath);
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
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ArteryScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }
    }
}
