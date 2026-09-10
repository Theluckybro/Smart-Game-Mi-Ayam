using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>
/// Pemeriksaan otomatis: nilai Inspector, NavMesh, dan logika NPCSensor.
/// Menu: Praktikum 2 -> Verify Scene
/// </summary>
public static class Praktikum02Verify
{
    private static int failed;

    [MenuItem("Praktikum 2/Verify Scene")]
    public static void Run()
    {
        failed = 0;
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/Praktikum02_NPCGuard.unity", OpenSceneMode.Single);

        GameObject player = GameObject.Find("Player");
        GameObject npc = GameObject.Find("NPC_Guard");
        GameObject wall = GameObject.Find("Wall");
        GameObject ground = GameObject.Find("Ground");
        GameObject nav = GameObject.Find("Navigation");
        GameObject patrol = GameObject.Find("PatrolPoints");

        Check("Ground ada", ground != null);
        Check("Ground scale 3,1,3", ground.transform.localScale == new Vector3(3f, 1f, 3f));
        Check("Player ada", player != null);
        Check("Player tag = Player", player.CompareTag("Player"));
        Check("Player layer = Player", LayerMask.LayerToName(player.layer) == "Player");
        Check("Player pos 0,1,0", player.transform.position == new Vector3(0f, 1f, 0f));
        Check("Wall layer = Obstacle", LayerMask.LayerToName(wall.layer) == "Obstacle");
        Check("Wall pos 0,1.5,3", wall.transform.position == new Vector3(0f, 1.5f, 3f));
        Check("Wall scale 6,3,0.5", wall.transform.localScale == new Vector3(6f, 3f, 0.5f));
        Check("PatrolPoints punya 4 child", patrol != null && patrol.transform.childCount == 4);
        Check("DirectionMarker child NPC", npc.transform.Find("DirectionMarker") != null);

        var marker = npc.transform.Find("DirectionMarker");
        Check("Marker local pos 0,0.5,0.65", marker.localPosition == new Vector3(0f, 0.5f, 0.65f));
        Check("Marker scale 0.2,0.2,0.5", marker.localScale == new Vector3(0.2f, 0.2f, 0.5f));

        // ---- PlayerController ----
        var pc = player.GetComponent<PlayerController>();
        Check("PlayerController terpasang", pc != null);
        var soPc = new SerializedObject(pc);
        CheckF("moveSpeed = 5", soPc, "moveSpeed", 5f);
        CheckF("rotationSpeed = 10", soPc, "rotationSpeed", 10f);

        // ---- NavMeshAgent ----
        var agent = npc.GetComponent<NavMeshAgent>();
        Check("NavMeshAgent terpasang", agent != null);
        Check("agent.speed = 3", Mathf.Approximately(agent.speed, 3f));
        Check("agent.angularSpeed = 360", Mathf.Approximately(agent.angularSpeed, 360f));
        Check("agent.acceleration = 8", Mathf.Approximately(agent.acceleration, 8f));
        Check("agent.stoppingDistance = 0.3", Mathf.Approximately(agent.stoppingDistance, 0.3f));

        // ---- NPCSensor ----
        var sensor = npc.GetComponent<NPCSensor>();
        Check("NPCSensor terpasang", sensor != null);
        var soSn = new SerializedObject(sensor);
        Check("sensor.player -> Player",
            soSn.FindProperty("player").objectReferenceValue == player.transform);
        CheckF("viewRadius = 8", soSn, "viewRadius", 8f);
        CheckF("viewAngle = 90", soSn, "viewAngle", 90f);
        CheckF("eyeHeight = 1.2", soSn, "eyeHeight", 1.2f);
        Check("obstacleMask = Obstacle",
            soSn.FindProperty("obstacleMask").intValue == 1 << LayerMask.NameToLayer("Obstacle"));

        // ---- NPCBrain ----
        var brain = npc.GetComponent<NPCBrain>();
        Check("NPCBrain terpasang", brain != null);
        var soBr = new SerializedObject(brain);
        Check("brain.sensor -> NPCSensor", soBr.FindProperty("sensor").objectReferenceValue == sensor);
        Check("brain.agent -> NavMeshAgent", soBr.FindProperty("agent").objectReferenceValue == agent);
        var pp = soBr.FindProperty("patrolPoints");
        Check("patrolPoints size = 4", pp.arraySize == 4);
        for (int i = 0; i < pp.arraySize; i++)
        {
            var t = pp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            Check("Element " + i + " = Point" + (i + 1), t != null && t.name == "Point" + (i + 1));
        }
        CheckF("waypointTolerance = 0.7", soBr, "waypointTolerance", 0.7f);
        CheckF("patrolSpeed = 2", soBr, "patrolSpeed", 2f);
        CheckF("chaseSpeed = 4", soBr, "chaseSpeed", 4f);
        CheckF("searchDuration = 4", soBr, "searchDuration", 4f);
        CheckF("searchTolerance = 0.8", soBr, "searchTolerance", 0.8f);

        // ---- NavMesh ----
        var surface = nav.GetComponent<NavMeshSurface>();
        Check("NavMeshSurface terpasang", surface != null);
        Check("NavMeshData tersimpan sebagai asset",
            surface.navMeshData != null && AssetDatabase.Contains(surface.navMeshData));

        NavMeshHit hit;
        Check("NPC start di atas NavMesh",
            NavMesh.SamplePosition(npc.transform.position, out hit, 1.5f, NavMesh.AllAreas));
        for (int i = 0; i < patrol.transform.childCount; i++)
        {
            Transform p = patrol.transform.GetChild(i);
            Check(p.name + " di atas NavMesh",
                NavMesh.SamplePosition(p.position, out hit, 1.5f, NavMesh.AllAreas));
        }

        // ---- Uji logika sensor (Update dipanggil lewat reflection) ----
        Vector3 npcHome = npc.transform.position;
        Quaternion npcRot = npc.transform.rotation;
        Vector3 playerHome = player.transform.position;

        Check("Sensor: player 3m di depan -> TERLIHAT",
            See(sensor, npc, player, new Vector3(-6f, 1f, -6f), 90f, new Vector3(-3f, 1f, -6f)));
        Check("Sensor: player di belakang -> TIDAK terlihat",
            !See(sensor, npc, player, new Vector3(-6f, 1f, -6f), 90f, new Vector3(-9f, 1f, -6f)));
        Check("Sensor: player 11m (di luar radius) -> TIDAK terlihat",
            !See(sensor, npc, player, new Vector3(-6f, 1f, -6f), 90f, new Vector3(5f, 1f, -6f)));
        Check("Sensor: player 2m di depan, sebelum Wall -> TERLIHAT",
            See(sensor, npc, player, new Vector3(0f, 1f, 0f), 0f, new Vector3(0f, 1f, 2f)));
        Check("Sensor: player 5m di depan, di balik Wall -> TIDAK terlihat",
            !See(sensor, npc, player, new Vector3(0f, 1f, 0f), 0f, new Vector3(0f, 1f, 5f)));

        npc.transform.SetPositionAndRotation(npcHome, npcRot);
        player.transform.position = playerHome;

        if (failed > 0)
        {
            Debug.LogError("[VERIFY] GAGAL: " + failed + " pemeriksaan tidak lolos.");
            EditorApplication.Exit(1);
        }
        Debug.Log("[VERIFY] SEMUA PEMERIKSAAN LOLOS.");
    }

    private static bool See(NPCSensor sensor, GameObject npc, GameObject player,
        Vector3 npcPos, float yaw, Vector3 playerPos)
    {
        npc.transform.SetPositionAndRotation(npcPos, Quaternion.Euler(0f, yaw, 0f));
        player.transform.position = playerPos;
        Physics.SyncTransforms();

        typeof(NPCSensor)
            .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(sensor, null);

        return sensor.CanSeePlayer;
    }

    private static void CheckF(string label, SerializedObject so, string field, float expected)
    {
        Check(label, Mathf.Approximately(so.FindProperty(field).floatValue, expected));
    }

    private static void Check(string label, bool ok)
    {
        if (!ok) failed++;
        Debug.Log("[VERIFY] " + (ok ? "PASS" : "FAIL") + " - " + label);
    }
}
