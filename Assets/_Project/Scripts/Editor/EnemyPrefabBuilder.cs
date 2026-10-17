using AIGame.AI;
using AIGame.Gameplay;
using UnityEditor;
using UnityEngine;

namespace AIGame.Editor
{
    public static class EnemyPrefabBuilder
    {
        [MenuItem("AIGame/Create Enemy Prefabs")]
        public static void CreateAllEnemyPrefabs()
        {
            CreateEnemyPrefab(EnemyArchetype.MacrophageSwarm, "Enemy_Macrophage", new Color(1f, 0.5f, 0.25f));
            CreateEnemyPrefab(EnemyArchetype.AntibodyNet, "Enemy_Antibody", new Color(0.3f, 0.8f, 1f));
            CreateEnemyPrefab(EnemyArchetype.BioelectricDisruptor, "Enemy_Disruptor", new Color(1f, 0.2f, 1f));

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Done", "All enemy prefabs created:\n- Enemy_Macrophage.prefab\n- Enemy_Antibody.prefab\n- Enemy_Disruptor.prefab", "OK");
        }

        private static void CreateEnemyPrefab(EnemyArchetype archetype, string prefabName, Color meshColor)
        {
            string prefabPath = $"Assets/_Project/Prefabs/{prefabName}.prefab";

            // Check if already exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log($"[EnemyPrefabBuilder] {prefabName} already exists at {prefabPath}");
                return;
            }

            GameObject enemy = new GameObject(prefabName);
            SphereCollider col = enemy.AddComponent<SphereCollider>();
            col.radius = 0.6f;

            Rigidbody rb = enemy.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            EnemyPerception perception = enemy.AddComponent<EnemyPerception>();
            EnemyAgent agent = enemy.AddComponent<EnemyAgent>();
            enemy.AddComponent<EnemyDebugOverlay>();

            SerializedObject agentSO = new SerializedObject(agent);
            agentSO.FindProperty("archetype").enumValueIndex = (int)archetype;
            agentSO.ApplyModifiedPropertiesWithoutUndo();

            GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meshObj.name = "Mesh";
            meshObj.transform.SetParent(enemy.transform, false);
            meshObj.transform.localScale = Vector3.one * 0.8f;
            Object.DestroyImmediate(meshObj.GetComponent<SphereCollider>());

            System.IO.Directory.CreateDirectory("Assets/_Project/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(enemy, prefabPath);
            Object.DestroyImmediate(enemy);

            Debug.Log($"[EnemyPrefabBuilder] Created {prefabName} prefab at {prefabPath}");
        }
    }
}
