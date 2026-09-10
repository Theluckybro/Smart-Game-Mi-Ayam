using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>
/// Membangun scene "Praktikum02_NPCGuard" persis seperti langkah 5-27 & 33-41
/// pada MODUL PRAKTIKUM 2 - NPC Guard: Sensor + Memory + Decision.
/// Menu: Praktikum 2 -> Build Scene Praktikum02_NPCGuard
/// </summary>
public static class Praktikum02SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Praktikum02_NPCGuard.unity";
    private const string MaterialDir = "Assets/Materials";
    private const string NavMeshDir = "Assets/Scenes/Praktikum02_NPCGuard";
    private const string NavMeshDataPath = NavMeshDir + "/NavMesh-Navigation.asset";

    [MenuItem("Praktikum 2/Build Scene Praktikum02_NPCGuard")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.DefaultGameObjects,
            NewSceneMode.Single);

        // ---------- Kamera overview (modul tidak menentukan transform kamera) ----------
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 12f, -12f);
            cam.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
        }

        // ---------- 6 & 7. Ground + GroundMaterial ----------
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
        Paint(ground, CreateMaterial("GroundMaterial", new Color(0.33f, 0.42f, 0.33f)));

        // ---------- 8, 9, 10, 11, 15. Player ----------
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Player");
        Paint(player, CreateMaterial("PlayerMaterial", new Color(0.16f, 0.35f, 0.85f)));

        var controller = player.AddComponent<PlayerController>();
        var soController = new SerializedObject(controller);
        soController.FindProperty("moveSpeed").floatValue = 5f;
        soController.FindProperty("rotationSpeed").floatValue = 10f;
        soController.ApplyModifiedPropertiesWithoutUndo();

        // ---------- 18 & 19. Wall ----------
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.position = new Vector3(0f, 1.5f, 3f);
        wall.transform.localScale = new Vector3(6f, 3f, 0.5f);
        wall.layer = LayerMask.NameToLayer("Obstacle");
        Paint(wall, CreateMaterial("WallMaterial", new Color(0.55f, 0.55f, 0.55f)));

        // ---------- 20, 21, 22. NPC_Guard + DirectionMarker ----------
        GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = "NPC_Guard";
        npc.transform.position = new Vector3(-6f, 1f, -6f);
        Paint(npc, CreateMaterial("NPCMaterial", new Color(0.85f, 0.16f, 0.16f)));

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "DirectionMarker";
        marker.transform.SetParent(npc.transform, false);
        marker.transform.localPosition = new Vector3(0f, 0.5f, 0.65f);
        marker.transform.localScale = new Vector3(0.2f, 0.2f, 0.5f);

        // ---------- 26. NavMeshAgent ----------
        var agent = npc.AddComponent<NavMeshAgent>();
        agent.speed = 3f;
        agent.angularSpeed = 360f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.3f;

        // ---------- 27. Patrol Points ----------
        var patrolRoot = new GameObject("PatrolPoints");
        Vector3[] positions =
        {
            new Vector3(-8f, 0f, -8f),
            new Vector3( 8f, 0f, -8f),
            new Vector3( 8f, 0f,  8f),
            new Vector3(-8f, 0f,  8f)
        };
        var points = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var p = new GameObject("Point" + (i + 1));
            p.transform.SetParent(patrolRoot.transform, false);
            p.transform.position = positions[i];
            points[i] = p.transform;
        }

        // ---------- 33 & 41. NPCSensor ----------
        var sensor = npc.AddComponent<NPCSensor>();
        var soSensor = new SerializedObject(sensor);
        soSensor.FindProperty("player").objectReferenceValue = player.transform;
        soSensor.FindProperty("viewRadius").floatValue = 8f;
        soSensor.FindProperty("viewAngle").floatValue = 90f;
        soSensor.FindProperty("obstacleMask").intValue = 1 << LayerMask.NameToLayer("Obstacle");
        soSensor.FindProperty("eyeHeight").floatValue = 1.2f;
        soSensor.ApplyModifiedPropertiesWithoutUndo();

        // ---------- 39, 40, 41. NPCBrain ----------
        var brain = npc.AddComponent<NPCBrain>();
        var soBrain = new SerializedObject(brain);
        soBrain.FindProperty("sensor").objectReferenceValue = sensor;
        soBrain.FindProperty("agent").objectReferenceValue = agent;

        SerializedProperty patrolProp = soBrain.FindProperty("patrolPoints");
        patrolProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
            patrolProp.GetArrayElementAtIndex(i).objectReferenceValue = points[i];

        soBrain.FindProperty("waypointTolerance").floatValue = 0.7f;
        soBrain.FindProperty("patrolSpeed").floatValue = 2f;
        soBrain.FindProperty("chaseSpeed").floatValue = 4f;
        soBrain.FindProperty("searchDuration").floatValue = 4f;
        soBrain.FindProperty("searchTolerance").floatValue = 0.8f;
        soBrain.ApplyModifiedPropertiesWithoutUndo();

        // ---------- 24. Navigation + NavMesh Surface ----------
        var navigation = new GameObject("Navigation");
        var surface = navigation.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

        // Urutan hierarchy sesuai bagian 77 pada modul.
        ground.transform.SetSiblingIndex(2);
        navigation.transform.SetSiblingIndex(3);
        player.transform.SetSiblingIndex(4);
        npc.transform.SetSiblingIndex(5);
        wall.transform.SetSiblingIndex(6);
        patrolRoot.transform.SetSiblingIndex(7);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // ---------- 25. Bake NavMesh ----------
        // Player & NPC dinonaktifkan sementara supaya kapsulnya tidak terpahat
        // menjadi lubang di NavMesh (modul: "Ground akan menjadi area navigasi NPC").
        player.SetActive(false);
        npc.SetActive(false);
        surface.BuildNavMesh();
        player.SetActive(true);
        npc.SetActive(true);

        // BuildNavMesh() hanya membangun NavMeshData di memori. Supaya hasil bake
        // ikut tersimpan bersama scene, NavMeshData disimpan sebagai asset.
        NavMeshData data = surface.navMeshData;
        if (data == null)
            throw new System.Exception("Bake NavMesh gagal: navMeshData null.");

        if (!AssetDatabase.IsValidFolder(NavMeshDir))
            AssetDatabase.CreateFolder("Assets/Scenes", "Praktikum02_NPCGuard");

        data.name = "NavMesh-Navigation";
        AssetDatabase.CreateAsset(data, NavMeshDataPath);
        AssetDatabase.SaveAssets();

        var soSurface = new SerializedObject(surface);
        soSurface.FindProperty("m_NavMeshData").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshDataPath);
        soSurface.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log("[Praktikum 2] Scene siap: " + ScenePath +
                  " | NavMesh triangles: " + NavMesh.CalculateTriangulation().indices.Length / 3);
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var material = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static void Paint(GameObject go, Material material)
    {
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
