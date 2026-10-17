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
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.06f, 0.14f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.42f, 0.06f, 0.18f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0125f;
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
            GameObject arteryRoot = new GameObject("ArteryTunnel");

            Material wallMat = CreateOrUpdateEnemyMaterial("ArteryWall", new Color(0.72f, 0.12f, 0.2f));
            wallMat.EnableKeyword("_EMISSION");
            wallMat.SetColor("_EmissionColor", new Color(0.32f, 0.05f, 0.11f));
            wallMat.SetFloat("_Glossiness", 0.08f);

            Material glowMat = CreateOrUpdateTransparentMaterial("ArteryGlow", new Color(0.95f, 0.22f, 0.62f, 0.08f));
            Material bloodMat = CreateOrUpdateEnemyMaterial("BloodCell", new Color(0.72f, 0.08f, 0.14f));

            GameObject tubeObj = new GameObject("ArteryTubeMesh");
            tubeObj.transform.SetParent(arteryRoot.transform, false);
            tubeObj.transform.position = shipOrigin;

            MeshFilter meshFilter = tubeObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = tubeObj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = wallMat;
            meshFilter.sharedMesh = BuildArteryTubeMesh(ArteryPath.Length, ArteryPath.RadiusX, ArteryPath.RadiusY, 80, 36);

            // Add a static Rigidbody to make the tube a collider-only body
            Rigidbody tubeRb = tubeObj.AddComponent<Rigidbody>();
            tubeRb.isKinematic = true;
            tubeRb.useGravity = false;

            // Configure MeshCollider with proper cooking options for hollow mesh
            MeshCollider tubeCollider = tubeObj.AddComponent<MeshCollider>();
            tubeCollider.sharedMesh = null; // Reset before reassigning
            tubeCollider.convex = false;
            tubeCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.WeldColocatedVertices;
            tubeCollider.sharedMesh = meshFilter.sharedMesh;

            // Subtle glow layers inside the tube.
            for (int i = 0; i < 12; i++)
            {
                GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glow.name = "ArteryGlow_" + i;
                glow.transform.SetParent(arteryRoot.transform, false);
                glow.transform.position = shipOrigin + new Vector3(0f, 0f, 8f + i * 12f);
                glow.transform.localScale = new Vector3(22f, 13.5f, 15f);

                Renderer renderer = glow.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = glowMat;
                }

                Collider glowCollider = glow.GetComponent<Collider>();
                if (glowCollider != null)
                {
                    Object.DestroyImmediate(glowCollider);
                }
            }

            // Add floating red blood cells for visual motion and scale cues.
            for (int i = 0; i < 42; i++)
            {
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cell.name = "BloodCell_" + i;
                cell.transform.SetParent(arteryRoot.transform, false);

                float rz = Random.Range(14f, 140f);
                Vector3 center = ArteryPath.CenterAt(rz);
                Vector2 radii = ArteryPath.RadiiAt(rz);
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radial01 = Mathf.Sqrt(Random.value) * 0.72f;
                float rx = center.x + Mathf.Cos(angle) * radii.x * radial01;
                float ry = center.y + Mathf.Sin(angle) * radii.y * radial01;

                cell.transform.position = shipOrigin + new Vector3(rx, ry, rz);
                cell.transform.localScale = new Vector3(1.15f, 0.3f, 0.95f);
                cell.transform.rotation = Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 360f), Random.Range(-20f, 20f));

                Renderer renderer = cell.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = bloodMat;
                }

                Collider cellCollider = cell.GetComponent<Collider>();
                if (cellCollider != null)
                {
                    Object.DestroyImmediate(cellCollider);
                }
            }
        }

        private static Mesh BuildArteryTubeMesh(float length, float radiusX, float radiusY, int ringCount, int sides)
        {
            Mesh mesh = new Mesh { name = "ArteryTube" };

            int vertexCount = ringCount * sides;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];

            int quadCount = (ringCount - 1) * sides;
            int[] triangles = new int[quadCount * 12]; // double-sided
            int triIndex = 0;

            for (int ring = 0; ring < ringCount; ring++)
            {
                float v = ringCount <= 1 ? 0f : ring / (float)(ringCount - 1);
                float z = v * length;

                // Same shape the pilot steers by (see ArteryPath).
                Vector3 center = ArteryPath.CenterAt(z);
                Vector2 radii = ArteryPath.RadiiAt(z);
                float rx = radii.x * (radiusX / ArteryPath.RadiusX);
                float ry = radii.y * (radiusY / ArteryPath.RadiusY);

                for (int side = 0; side < sides; side++)
                {
                    float u = side / (float)sides;
                    float angle = u * Mathf.PI * 2f;
                    int index = ring * sides + side;

                    float x = Mathf.Cos(angle) * rx;
                    float y = Mathf.Sin(angle) * ry;

                    vertices[index] = center + new Vector3(x, y, 0f);
                    normals[index] = -new Vector3(Mathf.Cos(angle) / Mathf.Max(rx, 0.001f), Mathf.Sin(angle) / Mathf.Max(ry, 0.001f), 0f).normalized;
                    uvs[index] = new Vector2(u, v);

                    if (ring >= ringCount - 1)
                    {
                        continue;
                    }

                    int nextSide = (side + 1) % sides;
                    int current = index;
                    int currentNext = ring * sides + nextSide;
                    int upper = (ring + 1) * sides + side;
                    int upperNext = (ring + 1) * sides + nextSide;

                    // Front-facing for inside
                    triangles[triIndex++] = current;
                    triangles[triIndex++] = upper;
                    triangles[triIndex++] = currentNext;
                    triangles[triIndex++] = currentNext;
                    triangles[triIndex++] = upper;
                    triangles[triIndex++] = upperNext;

                    // Reverse side for robustness
                    triangles[triIndex++] = currentNext;
                    triangles[triIndex++] = upper;
                    triangles[triIndex++] = current;
                    triangles[triIndex++] = upperNext;
                    triangles[triIndex++] = upper;
                    triangles[triIndex++] = currentNext;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateOrUpdateTransparentMaterial(string materialName, Color color)
        {
            Directory.CreateDirectory("Assets/_Project/Materials");
            string matPath = $"Assets/_Project/Materials/{materialName}.mat";

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.SetFloat("_Mode", 2f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.color = color;
            mat.SetFloat("_Glossiness", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 0.95f);
            EditorUtility.SetDirty(mat);
            return mat;
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
