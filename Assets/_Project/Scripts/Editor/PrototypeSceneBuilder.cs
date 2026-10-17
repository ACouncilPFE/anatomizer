using AIGame.AI;
using AIGame.Core;
using AIGame.Gameplay;
using AIGame.Systems;
using AIGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;

namespace AIGame.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission_01_Artery.unity";

        [MenuItem("AIGame/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Build Prototype Scene",
                "This will create and save a new scene at:\n" + ScenePath + "\n\nExisting scene content will be replaced. Proceed?",
                "Build",
                "Cancel");

            if (!confirmed)
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateDirectionalLight();
            GameObject camObj = CreateCamera();
            GameObject shipRoot = CreateShip();
            ConfigureArteryAtmosphere();
            CreateArteryEnvironment(shipRoot.transform.position);

            // Wire the camera to follow the ship
            CameraFollow follow = camObj.GetComponent<CameraFollow>();
            SerializedObject camSO = new SerializedObject(follow);
            camSO.FindProperty("target").objectReferenceValue = shipRoot.transform;
            camSO.ApplyModifiedPropertiesWithoutUndo();

            CreateGameManager();
            GameObject spawnerObj = CreateSpawner(shipRoot);
            CreateHUD();
            CreateObjectiveManager();
            CreateZoneTriggerExample();

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Create or update assets
            CreateAllEnemyPrefabs();
            Projectile projectilePrefab = CreateOrUpdateProjectilePrefab();
            MissionZoneSpawnProfile spawnProfile = CreateOrUpdateSpawnProfile(projectilePrefab);

            // Wire spawner to profile
            MissionZoneSpawner spawner = Object.FindObjectOfType<MissionZoneSpawner>();
            if (spawner != null && spawnProfile != null)
            {
                // Collect spawn points
                Transform[] spawnPoints = new Transform[spawnerObj.transform.childCount];
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    spawnPoints[i] = spawnerObj.transform.GetChild(i);
                }

                SerializedObject so = new SerializedObject(spawner);
                so.FindProperty("zoneBindings").arraySize = 1;
                SerializedProperty binding = so.FindProperty("zoneBindings").GetArrayElementAtIndex(0);
                binding.FindPropertyRelative("profile").objectReferenceValue = spawnProfile;

                // Wire spawn points
                SerializedProperty pointsArray = binding.FindPropertyRelative("spawnPoints");
                pointsArray.arraySize = spawnPoints.Length;
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    pointsArray.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Wire weapon system to projectile prefab
            WeaponSystem weapon = shipRoot.GetComponent<WeaponSystem>();
            if (weapon != null && projectilePrefab != null)
            {
                SerializedObject weaponSO = new SerializedObject(weapon);
                weaponSO.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
                weaponSO.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.Refresh();

            Debug.Log("[PrototypeSceneBuilder] Scene built and saved to: " + ScenePath);
            EditorUtility.DisplayDialog("Done", "Prototype scene created at:\n" + ScenePath + "\n\nPress Play to run. Left-click to fire.", "OK");
        }

        private static void CreateDirectionalLight()
        {
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.65f;
            light.color = new Color(1f, 0.72f, 0.82f);
            lightObj.transform.rotation = Quaternion.Euler(28f, -8f, 0f);
            GameObjectUtility.SetStaticEditorFlags(lightObj, StaticEditorFlags.ContributeGI);
        }

        private static GameObject CreateCamera()
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 360f;
            camObj.AddComponent<AudioListener>();
            CameraFollow follow = camObj.AddComponent<CameraFollow>();
            SerializedObject followSO = new SerializedObject(follow);
            followSO.FindProperty("offset").vector3Value = new Vector3(0f, 2.4f, -7.2f);
            followSO.FindProperty("positionSmoothTime").floatValue = 0.18f;
            followSO.FindProperty("lookSmoothTime").floatValue = 0.12f;
            followSO.FindProperty("lookAheadDistance").floatValue = 7f;
            followSO.ApplyModifiedPropertiesWithoutUndo();
            camObj.transform.position = new Vector3(0f, 2.8f, -7.5f);
            camObj.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            return camObj;
        }

        private static void ConfigureArteryAtmosphere()
        {
            ArteryEnvironment.ApplyAtmosphere();
        }

        private static GameObject CreateShip()
        {
            GameObject ship = new GameObject("Ship");
            ship.tag = "Player";

            Rigidbody rb = ship.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 2.5f; // Lighter for responsiveness
            rb.drag = 0f; // ShipController manages damping via velocity lerp
            rb.angularDrag = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Non-convex mesh needs this, not Speculative
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.None; // Allow all motion

            ship.AddComponent<ShipController>();
            ship.AddComponent<ShipHealth>();
            ship.AddComponent<ShipSystems>();
            ship.AddComponent<ShipPilot>();

            GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            meshObj.name = "Mesh";
            meshObj.transform.SetParent(ship.transform, false);
            meshObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            meshObj.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            Object.DestroyImmediate(meshObj.GetComponent<CapsuleCollider>());

            CapsuleCollider col = ship.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.35f;
            col.direction = 2;
            col.material = CreateOrUpdatePhysicsMaterial("Ship_Frictionless");

            GameObject firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(ship.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, 0f, 1.2f);

            WeaponSystem weapon = ship.AddComponent<WeaponSystem>();
            SerializedObject weaponSO = new SerializedObject(weapon);
            weaponSO.FindProperty("firePoint").objectReferenceValue = firePoint.transform;
            weaponSO.FindProperty("fireRate").floatValue = 8f;
            weaponSO.ApplyModifiedPropertiesWithoutUndo();

            // Co-pilot borrows the main gun's projectile and fire point.
            ship.AddComponent<ShipCoPilot>();
            ship.AddComponent<SutureBeam>();

            ship.transform.position = new Vector3(0f, 0f, 0f);
            ship.transform.rotation = Quaternion.identity;
            return ship;
        }

        private static void CreateGameManager()
        {
            GameObject gmObj = new GameObject("GameManager");
            GameManager gm = gmObj.AddComponent<GameManager>();
            SerializedObject gmSO = new SerializedObject(gm);
            gmSO.FindProperty("missionDurationSeconds").floatValue = 1800f;
            gmSO.FindProperty("requiredObjectives").intValue = 3;
            gmSO.FindProperty("autoStartMission").boolValue = true;
            gmSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateSpawner(GameObject shipRoot)
        {
            GameObject spawnerObj = new GameObject("ZoneSpawner");
            spawnerObj.AddComponent<MissionZoneSpawner>();

            // Spawn enemies in a forward fan so encounters start in front of the player.
            Vector3[] offsets =
            {
                new Vector3(0f, 0f, 34f),
                new Vector3(-8f, 0f, 30f),
                new Vector3(8f, 0f, 30f),
                new Vector3(0f, 0f, 40f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject sp = new GameObject("SpawnPoint_" + i);
                sp.transform.SetParent(spawnerObj.transform, false);
                sp.transform.position = shipRoot.transform.position + shipRoot.transform.TransformDirection(offsets[i]);
            }

            Debug.Log("[PrototypeSceneBuilder] ZoneSpawner created and configured.");
            return spawnerObj;
        }

        private static void CreateAllEnemyPrefabs()
        {
            CreateEnemyPrefabVariant(EnemyArchetype.MacrophageSwarm, "Enemy_Macrophage", new Color(1f, 0.5f, 0.25f));
            CreateEnemyPrefabVariant(EnemyArchetype.AntibodyNet, "Enemy_Antibody", new Color(0.3f, 0.8f, 1f));
            CreateEnemyPrefabVariant(EnemyArchetype.BioelectricDisruptor, "Enemy_Disruptor", new Color(1f, 0.2f, 1f));
        }

        private static void CreateEnemyPrefabVariant(EnemyArchetype archetype, string prefabName, Color meshColor)
        {
            string prefabPath = $"Assets/_Project/Prefabs/{prefabName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            GameObject enemy = new GameObject(prefabName);
            SphereCollider col = enemy.AddComponent<SphereCollider>();
            col.radius = 0.6f;

            Rigidbody rb = enemy.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            enemy.AddComponent<EnemyPerception>();
            EnemyAgent agent = enemy.AddComponent<EnemyAgent>();
            enemy.AddComponent<EnemyDebugOverlay>();

            SerializedObject agentSO = new SerializedObject(agent);
            agentSO.FindProperty("archetype").enumValueIndex = (int)archetype;
            agentSO.ApplyModifiedPropertiesWithoutUndo();

            GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meshObj.name = "Mesh";
            meshObj.transform.SetParent(enemy.transform, false);
            meshObj.transform.localScale = new Vector3(2.2f, 1.1f, 1.55f);
            Object.DestroyImmediate(meshObj.GetComponent<SphereCollider>());

            Renderer renderer = meshObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateOrUpdateEnemyMaterial(prefabName, meshColor);
            }

            Directory.CreateDirectory("Assets/_Project/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(enemy, prefabPath);
            Object.DestroyImmediate(enemy);

            Debug.Log($"[PrototypeSceneBuilder] Created {prefabName} prefab");
        }

        private static void CreateSampleEnemyPrefab(GameObject spawnerParent)
        {
            CreateEnemyPrefabVariant(EnemyArchetype.MacrophageSwarm, "Enemy_Macrophage", new Color(1f, 0.5f, 0.25f));
        }

        private static Projectile CreateOrUpdateProjectilePrefab()
        {
            Directory.CreateDirectory("Assets/_Project/Prefabs");
            string prefabPath = "Assets/_Project/Prefabs/Projectile.prefab";
            Projectile existing = AssetDatabase.LoadAssetAtPath<Projectile>(prefabPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            GameObject projObj = new GameObject("Projectile");
            SphereCollider col = projObj.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.3f;
            Rigidbody rb = projObj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meshObj.name = "Mesh";
            meshObj.transform.SetParent(projObj.transform, false);
            meshObj.transform.localScale = Vector3.one * 0.3f;
            Object.DestroyImmediate(meshObj.GetComponent<SphereCollider>());

            projObj.AddComponent<Projectile>();

            PrefabUtility.SaveAsPrefabAsset(projObj, prefabPath);
            Object.DestroyImmediate(projObj);

            Projectile result = AssetDatabase.LoadAssetAtPath<Projectile>(prefabPath);
            Debug.Log("[PrototypeSceneBuilder] Projectile prefab created at: " + prefabPath);
            return result;
        }

        private static MissionZoneSpawnProfile CreateOrUpdateSpawnProfile(Projectile projectilePrefab)
        {
            Directory.CreateDirectory("Assets/_Project/ScriptableObjects");
            string profilePath = "Assets/_Project/ScriptableObjects/SpawnProfile_Artery.asset";
            MissionZoneSpawnProfile existingProfile = AssetDatabase.LoadAssetAtPath<MissionZoneSpawnProfile>(profilePath);
            if (existingProfile != null)
            {
                AssetDatabase.DeleteAsset(profilePath);
            }

            MissionZoneSpawnProfile profile = ScriptableObject.CreateInstance<MissionZoneSpawnProfile>();

            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Macrophage.prefab");
            if (enemyPrefab == null)
            {
                CreateSampleEnemyPrefab(null);
                enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Macrophage.prefab");
            }

            EnemyAgent enemyAgent = enemyPrefab != null ? enemyPrefab.GetComponent<EnemyAgent>() : null;

            profile.zone = MissionZone.Artery;
            profile.maxAlive = 3;
            profile.spawnIntervalSeconds = 5.5f;
            profile.enemyPrefab = enemyAgent;

            // Set archetype weights
            profile.archetypeWeights.Clear();
            profile.archetypeWeights.Add(new ZoneArchetypeWeight { archetype = EnemyArchetype.MacrophageSwarm, weight = 60f });
            profile.archetypeWeights.Add(new ZoneArchetypeWeight { archetype = EnemyArchetype.AntibodyNet, weight = 25f });
            profile.archetypeWeights.Add(new ZoneArchetypeWeight { archetype = EnemyArchetype.BioelectricDisruptor, weight = 15f });

            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.Refresh();

            Debug.Log("[PrototypeSceneBuilder] Spawn profile created at: " + profilePath);
            return profile;
        }

        private static Material CreateOrUpdateEnemyMaterial(string prefabName, Color color)
        {
            Directory.CreateDirectory("Assets/_Project/Materials");
            string matPath = $"Assets/_Project/Materials/{prefabName}_Mat.mat";

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.color = color;
            mat.SetFloat("_Glossiness", 0.18f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static PhysicMaterial CreateOrUpdatePhysicsMaterial(string materialName)
        {
            Directory.CreateDirectory("Assets/_Project/PhysicsMaterials");
            string matPath = $"Assets/_Project/PhysicsMaterials/{materialName}.physicMaterial";

            PhysicMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(matPath);
            if (mat == null)
            {
                mat = new PhysicMaterial();
                mat.name = materialName;
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.dynamicFriction = 0.1f;
            mat.staticFriction = 0.1f;
            mat.bounciness = 0f;
            mat.frictionCombine = PhysicMaterialCombine.Multiply;
            mat.bounceCombine = PhysicMaterialCombine.Minimum;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CreateArteryEnvironment(Vector3 shipOrigin)
        {
            // The sealed vessel, its lining, the blood flow and the ship's endoscope light all come
            // from ArteryEnvironment (built from ArteryPath, which starts at the ship's origin).
            GameObject arteryRoot = new GameObject("ArteryTunnel");

            GameObject tubeObj = new GameObject(ArteryEnvironment.TunnelObjectName);
            tubeObj.transform.SetParent(arteryRoot.transform, false);

            // A kinematic body makes the tube a collider-only object.
            Rigidbody tubeRb = tubeObj.AddComponent<Rigidbody>();
            tubeRb.isKinematic = true;
            tubeRb.useGravity = false;

            // Builds the wall now so the scene view shows it; it is rebuilt fresh at play time.
            tubeObj.AddComponent<ArteryEnvironment>().BuildWall();
        }

        private static void CreateHUD()
        {
            GameObject hud = new GameObject("HUD");
            hud.AddComponent<GameHUD>();
            hud.AddComponent<MissionZoneSpawnerHUD>();
        }

        private static void CreateObjectiveManager()
        {
            GameObject objMgrObj = new GameObject("ObjectiveManager");
            objMgrObj.AddComponent<ObjectiveManager>();
        }

        private static void CreateZoneTriggerExample()
        {
            GameObject triggerObj = new GameObject("ZoneTrigger_Artery");
            triggerObj.layer = LayerMask.NameToLayer("Default");
            BoxCollider box = triggerObj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(20f, 20f, 4f);
            triggerObj.transform.position = new Vector3(0f, 0f, 30f);

            MissionZoneTrigger trigger = triggerObj.AddComponent<MissionZoneTrigger>();
            SerializedObject triggerSO = new SerializedObject(trigger);
            triggerSO.FindProperty("zone").enumValueIndex = (int)MissionZone.Artery;
            triggerSO.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
