using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DungeonHunter.EditorTools
{
    /// <summary>
    /// Membangun scene prototype "Dungeon Hunter" secara otomatis dari peta ASCII.
    /// Menu: Dungeon Hunter → Build Prototype Scene
    ///
    /// Legenda peta (1 karakter = 1 sel 2x2 meter):
    ///   #  dinding        .  lantai          P  spawn player
    ///   M  melee enemy    R  ranged enemy    G  guardian
    ///   K  dungeon key    D  pintu guardian  A  artifact
    ///   w  waypoint       S  retreat point   L  obor / point light
    ///   T  jebakan duri (NavMesh area "Trap", cost tinggi)
    ///   g  gerbang besi (dynamic obstacle, menutup saat Key diambil)
    ///
    /// Struktur tambahan (didefinisikan lewat konstanta, bukan karakter peta):
    ///   Balkon setinggi 1.4 m di ruang barat + ramp naik + NavMeshLink untuk lompat turun.
    /// </summary>
    public static class DungeonSceneBuilder
    {
        private const float Cell = 2f;
        private const float WallHeight = 2.5f;

        // Balkon ruang barat (baris 9–11, kolom 1–5) + ramp (baris 9–10, kolom 6–7, naik ke arah barat)
        private const int BalconyRowMin = 9, BalconyRowMax = 11, BalconyColMin = 1, BalconyColMax = 5;
        private const int RampRowMin = 9, RampRowMax = 10, RampColMin = 6, RampColMax = 7;
        private const float BalconyHeight = 1.4f;

        private const string Root = "Assets/_DungeonHunter";
        private const string ScenePath = Root + "/Scenes/DungeonHunter_Progress.unity";
        private const string MaterialDir = Root + "/Materials";
        private const string PrefabDir = Root + "/Prefabs";

        private static readonly string[] Map =
        {
            "################################",
            "##########............##########",
            "##########.L........L.##########",
            "##########.....A......##########",
            "##########............##########",
            "##########.....G......##########",
            "##########.L........L.##########",
            "###############DD###############",
            "###############..###############",
            "#S......##............##.......#",
            "#.R.L...##.w..L....w..##...L.R.#",
            "#.......##...#....#...##.......#",
            "#.............TTTT....gg..#....#",
            "#.............TTTT....gg..#..K.#",
            "#.......##...#....#...##.......#",
            "#.......##.w..M...Mw..##......S#",
            "#.......##............##.......#",
            "###########..##############..###",
            "###########.........S...L....###",
            "########........################",
            "########...L....################",
            "########...P....################",
            "########........################",
            "########........################",
            "################################",
        };

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [MenuItem("Dungeon Hunter/Build Prototype Scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
        }

        /// <summary>Entry point untuk Unity batchmode (-executeMethod).</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(MaterialDir);
            EnsureFolder(PrefabDir);
            CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();

            // ---------- LEVEL GEOMETRY ----------
            var level = new GameObject("Level");
            BuildFloor(level.transform);
            BuildWalls(level.transform);
            BuildBalcony(level.transform);
            BuildTrapVolumes(level.transform);

            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            SaveNavMeshData(surface);

            BuildTorches();
            BuildJumpLink();
            BuildSpikeTraps();

            // ---------- AI POINTS ----------
            var points = new GameObject("AI Points").transform;
            List<Transform> waypoints = CellsOf('w').Select((p, i) => CreatePoint(points, $"Waypoint_{i}", p)).ToList();
            List<Transform> retreats = CellsOf('S').Select((p, i) => CreatePoint(points, $"RetreatPoint_{i}", p)).ToList();

            // ---------- PLAYER ----------
            GameObject player = CreatePlayer(CellsOf('P').First());

            // ---------- MANAGERS ----------
            var managers = new GameObject("Managers");
            var gm = managers.AddComponent<GameManager>();
            SetField(gm, "playerHealth", player.GetComponent<Health>());
            managers.AddComponent<GameHUD>();
            managers.AddComponent<AIDebugManager>();

            CreateCamera(player.transform);

            // ---------- ENEMIES ----------
            var enemies = new GameObject("Enemies").transform;
            Projectile projectilePrefab = CreateProjectilePrefab();

            int meleeIndex = 0;
            foreach (Vector3 p in CellsOf('M'))
            {
                var e = CreateMelee(enemies, p, meleeIndex + 1);
                List<Transform> route = OrderLoop(waypoints.Where(w => Vector3.Distance(w.position, p) < 18f).ToList());
                e.Setup($"Skeleton Warrior {meleeIndex + 1}", player.transform, route, retreats);
                e.SetStartWaypoint(meleeIndex * 2 % Mathf.Max(1, route.Count));
                meleeIndex++;
            }

            int rangedIndex = 0;
            foreach (Vector3 p in CellsOf('R'))
            {
                var e = CreateRanged(enemies, p, rangedIndex + 1, projectilePrefab);
                e.Setup($"Skeleton Archer {rangedIndex + 1}", player.transform, new List<Transform>(), retreats);
                rangedIndex++;
            }

            foreach (Vector3 p in CellsOf('G'))
            {
                var g = CreateGuardian(enemies, p);
                g.Setup("Crypt Guardian", player.transform, new List<Transform>(), new List<Transform>());
            }

            // ---------- OBJECTIVES ----------
            CreateKey(CellsOf('K').First());
            CreateDoor(Centroid(CellsOf('D')));
            CreateArtifact(CellsOf('A').First());
            CreatePortcullis(Centroid(CellsOf('g')));

            // ---------- SAVE ----------
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[DungeonSceneBuilder] Scene berhasil dibuat: {ScenePath}");
        }

        // =====================================================
        // MAP HELPERS
        // =====================================================

        private static int Rows => Map.Length;
        private static int Cols => Map[0].Length;

        private static Vector3 CellToWorld(int row, int col) =>
            new Vector3(col * Cell, 0f, (Rows - 1 - row) * Cell);

        private static bool OnBalcony(int row, int col) =>
            row >= BalconyRowMin && row <= BalconyRowMax && col >= BalconyColMin && col <= BalconyColMax;

        /// <summary>Posisi dunia sebuah sel, termasuk ketinggian lantai (balkon = 1.4 m).</summary>
        private static IEnumerable<Vector3> CellsOf(char c)
        {
            for (int r = 0; r < Rows; r++)
                for (int col = 0; col < Cols; col++)
                    if (Map[r][col] == c)
                        yield return CellToWorld(r, col) + Vector3.up * (OnBalcony(r, col) ? BalconyHeight : 0f);
        }

        private static Vector3 Centroid(IEnumerable<Vector3> pts)
        {
            List<Vector3> list = pts.ToList();
            Vector3 sum = Vector3.zero;
            foreach (Vector3 p in list)
                sum += p;
            return sum / list.Count;
        }

        /// <summary>Urutkan waypoint melingkar (berdasarkan sudut terhadap titik tengah) agar patroli membentuk loop.</summary>
        private static List<Transform> OrderLoop(List<Transform> pts)
        {
            if (pts.Count < 3)
                return pts;
            Vector3 c = Centroid(pts.Select(t => t.position));
            return pts.OrderBy(t => Mathf.Atan2(t.position.z - c.z, t.position.x - c.x)).ToList();
        }

        // =====================================================
        // LEVEL
        // =====================================================

        private static void BuildFloor(Transform parent)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent);
            float w = Cols * Cell;
            float d = Rows * Cell;
            floor.transform.position = new Vector3(w * 0.5f - Cell * 0.5f, -0.1f, d * 0.5f - Cell * 0.5f);
            floor.transform.localScale = new Vector3(w, 0.2f, d);
            floor.GetComponent<Renderer>().sharedMaterial = Materials["Floor"];
            floor.isStatic = true;
        }

        /// <summary>Dinding digabung per baris (run horizontal) supaya jumlah object sedikit.</summary>
        private static void BuildWalls(Transform parent)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(parent);

            for (int r = 0; r < Rows; r++)
            {
                int c = 0;
                while (c < Cols)
                {
                    if (Map[r][c] != '#')
                    {
                        c++;
                        continue;
                    }

                    int start = c;
                    while (c < Cols && Map[r][c] == '#')
                        c++;
                    int length = c - start;

                    if (!RunTouchesFloor(r, start, c - 1))
                        continue;

                    Vector3 a = CellToWorld(r, start);
                    Vector3 b = CellToWorld(r, c - 1);
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = $"Wall_{r}_{start}";
                    wall.transform.SetParent(walls);
                    wall.transform.position = (a + b) * 0.5f + Vector3.up * WallHeight * 0.5f;
                    wall.transform.localScale = new Vector3(length * Cell, WallHeight, Cell);
                    wall.GetComponent<Renderer>().sharedMaterial = Materials["Wall"];
                    wall.isStatic = true;

                    var modifier = wall.AddComponent<NavMeshModifier>();
                    modifier.overrideArea = true;
                    modifier.area = 1; // Not Walkable
                }
            }
        }

        private static bool RunTouchesFloor(int row, int c0, int c1)
        {
            for (int c = c0; c <= c1; c++)
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int rr = row + dr, cc = c + dc;
                        if (rr < 0 || rr >= Rows || cc < 0 || cc >= Cols)
                            continue;
                        if (Map[rr][cc] != '#')
                            return true;
                    }
            return false;
        }

        /// <summary>Balkon (platform tinggi) + ramp. Keduanya walkable dan ikut di-bake ke NavMesh.</summary>
        private static void BuildBalcony(Transform parent)
        {
            Vector3 nw = CellToWorld(BalconyRowMin, BalconyColMin) + new Vector3(-Cell * 0.5f, 0f, Cell * 0.5f);
            Vector3 se = CellToWorld(BalconyRowMax, BalconyColMax) + new Vector3(Cell * 0.5f, 0f, -Cell * 0.5f);
            var balcony = GameObject.CreatePrimitive(PrimitiveType.Cube);
            balcony.name = "Balcony";
            balcony.transform.SetParent(parent);
            balcony.transform.position = new Vector3((nw.x + se.x) * 0.5f, BalconyHeight * 0.5f, (nw.z + se.z) * 0.5f);
            balcony.transform.localScale = new Vector3(se.x - nw.x, BalconyHeight, nw.z - se.z);
            balcony.GetComponent<Renderer>().sharedMaterial = Materials["Balcony"];
            balcony.isStatic = true;

            Vector3 rampNw = CellToWorld(RampRowMin, RampColMin) + new Vector3(-Cell * 0.5f, 0f, Cell * 0.5f);
            Vector3 rampSe = CellToWorld(RampRowMax, RampColMax) + new Vector3(Cell * 0.5f, 0f, -Cell * 0.5f);
            float run = rampSe.x - rampNw.x;
            float angle = Mathf.Atan2(BalconyHeight, run) * Mathf.Rad2Deg;
            float slopeLength = Mathf.Sqrt(run * run + BalconyHeight * BalconyHeight);
            const float thickness = 0.2f;

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp";
            ramp.transform.SetParent(parent);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, -angle);
            ramp.transform.position = new Vector3((rampNw.x + rampSe.x) * 0.5f, BalconyHeight * 0.5f, (rampNw.z + rampSe.z) * 0.5f)
                                      - ramp.transform.up * thickness * 0.5f;
            ramp.transform.localScale = new Vector3(slopeLength, thickness, rampNw.z - rampSe.z);
            ramp.GetComponent<Renderer>().sharedMaterial = Materials["Balcony"];
            ramp.isStatic = true;
        }

        /// <summary>NavMeshModifierVolume: menandai lantai jebakan sebagai area "Trap" (cost tinggi).</summary>
        private static void BuildTrapVolumes(Transform parent)
        {
            int trapArea = NavMesh.GetAreaFromName("Trap");
            if (trapArea < 0)
                throw new InvalidOperationException("NavMesh area 'Trap' belum ada di Project Settings → Navigation → Areas");

            foreach (Rect r in TrapRects())
            {
                var go = new GameObject("TrapArea");
                go.transform.SetParent(parent);
                go.transform.position = new Vector3(r.center.x, 0f, r.center.y);
                var volume = go.AddComponent<NavMeshModifierVolume>();
                volume.size = new Vector3(r.width, 2f, r.height);
                volume.center = Vector3.zero;
                volume.area = trapArea;
            }
        }

        /// <summary>Gabungkan sel 'T' menjadi satu persegi panjang (x, z).</summary>
        private static IEnumerable<Rect> TrapRects()
        {
            List<Vector3> cells = CellsOf('T').ToList();
            if (cells.Count == 0)
                yield break;
            float minX = cells.Min(c => c.x) - Cell * 0.5f, maxX = cells.Max(c => c.x) + Cell * 0.5f;
            float minZ = cells.Min(c => c.z) - Cell * 0.5f, maxZ = cells.Max(c => c.z) + Cell * 0.5f;
            yield return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        /// <summary>NavMeshLink satu arah: lompat turun dari tepi selatan balkon ke lantai.</summary>
        private static void BuildJumpLink()
        {
            Vector3 left = CellToWorld(BalconyRowMax, BalconyColMin);
            Vector3 right = CellToWorld(BalconyRowMax, BalconyColMax);
            float edgeZ = left.z - Cell * 0.5f;

            var go = new GameObject("NavMeshLink_BalconyJump");
            go.transform.position = new Vector3((left.x + right.x) * 0.5f, 0f, edgeZ);
            var link = go.AddComponent<NavMeshLink>();
            // Lebar link dibuat lebih kecil dari lebar balkon dan ujungnya agak masuk ke NavMesh:
            // jika tepi link keluar dari NavMesh, link tidak tersambung ke graph pathfinding.
            link.startPoint = new Vector3(0f, BalconyHeight, 1.2f);
            link.endPoint = new Vector3(0f, 0f, -2.2f);
            link.width = (right.x - left.x) - 2f;
            link.bidirectional = false;
            link.area = NavMesh.GetAreaFromName("Jump");
        }

        private static void BuildSpikeTraps()
        {
            foreach (Rect r in TrapRects())
            {
                var trap = new GameObject("SpikeTrap");
                trap.transform.position = new Vector3(r.center.x, 0f, r.center.y);

                CreateVisual(PrimitiveType.Cube, trap.transform, new Vector3(0f, 0.01f, 0f),
                    new Vector3(r.width, 0.02f, r.height), "TrapPlate");

                var spikes = new GameObject("Spikes").transform;
                spikes.SetParent(trap.transform, false);
                spikes.localPosition = new Vector3(0f, -0.35f, 0f);
                for (float x = -r.width * 0.5f + 0.5f; x < r.width * 0.5f; x += 1f)
                    for (float z = -r.height * 0.5f + 0.5f; z < r.height * 0.5f; z += 1f)
                        CreateVisual(PrimitiveType.Cube, spikes, new Vector3(x, 0f, z), new Vector3(0.12f, 0.6f, 0.12f), "Metal");

                var spikeTrap = trap.AddComponent<SpikeTrap>();
                var so = new SerializedObject(spikeTrap);
                so.FindProperty("size").vector3Value = new Vector3(r.width, 1f, r.height);
                so.FindProperty("spikes").objectReferenceValue = spikes;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SaveNavMeshData(NavMeshSurface surface)
        {
            string path = Root + "/Scenes/DungeonHunter_NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
        }

        private static void SetupLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.2f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.07f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 45f;

            var sun = new GameObject("Moonlight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.55f, 0.6f, 0.85f);
            sun.intensity = 0.45f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        }

        private static void BuildTorches()
        {
            var torches = new GameObject("Torches").transform;
            int i = 0;
            foreach (Vector3 p in CellsOf('L'))
            {
                var go = new GameObject($"Torch_{i++}");
                go.transform.SetParent(torches);
                go.transform.position = p + Vector3.up * 2.2f;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.6f, 0.25f);
                light.range = 10f;
                light.intensity = 4f;

                var flame = CreateVisual(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.25f, "Flame");
                flame.transform.localPosition = Vector3.zero;
            }
        }

        // =====================================================
        // ACTORS
        // =====================================================

        private static GameObject CreatePlayer(Vector3 pos)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = pos;

            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;

            var health = player.AddComponent<Health>();
            SetField(health, "maxHealth", 100f);

            CreateVisual(PrimitiveType.Capsule, player.transform, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), "Player");
            CreateVisual(PrimitiveType.Cube, player.transform, new Vector3(0f, 1.5f, 0.35f), new Vector3(0.55f, 0.18f, 0.2f), "Visor");

            var swordPivot = new GameObject("SwordPivot").transform;
            swordPivot.SetParent(player.transform, false);
            swordPivot.localPosition = new Vector3(0f, 1.1f, 0f);
            swordPivot.localRotation = Quaternion.Euler(0f, -35f, 0f);
            CreateVisual(PrimitiveType.Cube, swordPivot, new Vector3(0.35f, 0f, 0.9f), new Vector3(0.1f, 0.1f, 1.3f), "Sword");

            player.AddComponent<HitFlash>();
            player.AddComponent<PlayerController>();
            var combat = player.AddComponent<PlayerCombat>();
            SetField(combat, "swordPivot", swordPivot);

            return player;
        }

        private static void CreateCamera(Transform player)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.02f, 0.05f);
            cam.fieldOfView = 50f;
            camGo.AddComponent<AudioListener>();

            var follow = camGo.AddComponent<TopDownCamera>();
            follow.SetTarget(player);
            Vector3 offset = new Vector3(0f, 16f, -9f);
            camGo.transform.position = player.position + offset;
            camGo.transform.rotation = Quaternion.LookRotation(-offset);
        }

        private static T CreateEnemyBase<T>(Transform parent, string objName, Vector3 pos, float radius, float height,
            float maxHp, string materialKey, float bodyScale) where T : EnemyAI
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.acceleration = 20f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.2f;

            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.height = height;
            col.radius = radius;

            var health = go.AddComponent<Health>();
            SetField(health, "maxHealth", maxHp);

            CreateVisual(PrimitiveType.Capsule, go.transform, new Vector3(0f, height * 0.5f, 0f),
                new Vector3(0.9f, 1f, 0.9f) * bodyScale, materialKey);
            CreateVisual(PrimitiveType.Cube, go.transform, new Vector3(0f, height * 0.75f, 0.38f * bodyScale),
                new Vector3(0.6f, 0.15f, 0.2f) * bodyScale, "Eyes");

            go.AddComponent<HitFlash>();
            go.AddComponent<AIPerception>();
            go.AddComponent<AIMemory>();
            return go.AddComponent<T>();
        }

        private static MeleeEnemy CreateMelee(Transform parent, Vector3 pos, int index)
        {
            var e = CreateEnemyBase<MeleeEnemy>(parent, $"Melee_{index}", pos, 0.45f, 2f, 60f, "Melee", 1f);
            e.GetComponent<AIPerception>().Configure(9f, 110f);
            CreateVisual(PrimitiveType.Cube, e.transform, new Vector3(0.45f, 1f, 0.5f), new Vector3(0.12f, 0.12f, 1f), "Metal");
            return e;
        }

        private static RangedEnemy CreateRanged(Transform parent, Vector3 pos, int index, Projectile prefab)
        {
            var e = CreateEnemyBase<RangedEnemy>(parent, $"Ranged_{index}", pos, 0.45f, 2f, 45f, "Ranged", 0.9f);
            e.GetComponent<AIPerception>().Configure(12f, 100f);
            SetField(e, "trapAreaCost", 8f);
            CreateVisual(PrimitiveType.Cube, e.transform, new Vector3(-0.45f, 1.1f, 0.35f), new Vector3(0.08f, 1.1f, 0.08f), "Wood");

            var muzzle = new GameObject("FirePoint").transform;
            muzzle.SetParent(e.transform, false);
            muzzle.localPosition = new Vector3(0f, 1.3f, 0.7f);
            e.SetProjectile(prefab, muzzle);
            return e;
        }

        private static GuardianEnemy CreateGuardian(Transform parent, Vector3 pos)
        {
            var g = CreateEnemyBase<GuardianEnemy>(parent, "Guardian", pos, 0.8f, 3f, 300f, "Guardian", 1.6f);
            g.GetComponent<AIPerception>().Configure(11f, 150f);
            SetField(g, "chaseSpeed", 3.2f);
            SetField(g, "patrolSpeed", 1.8f);
            SetField(g, "trapAreaCost", 1f);
            CreateVisual(PrimitiveType.Cube, g.transform, new Vector3(0.9f, 1.6f, 0.4f), new Vector3(0.6f, 0.6f, 0.9f), "Metal");
            CreateVisual(PrimitiveType.Cube, g.transform, new Vector3(-0.9f, 1.6f, 0.4f), new Vector3(0.6f, 0.6f, 0.9f), "Metal");
            return g;
        }

        private static Projectile CreateProjectilePrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 0.3f;
            go.GetComponent<Renderer>().sharedMaterial = Materials["Projectile"];
            go.AddComponent<Projectile>();

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.1f);
            light.range = 3f;
            light.intensity = 2f;

            string path = PrefabDir + "/Projectile.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Projectile>();
        }

        // =====================================================
        // OBJECTIVES
        // =====================================================

        private static void CreateKey(Vector3 pos)
        {
            var key = new GameObject("DungeonKey");
            key.transform.position = pos;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(key.transform, false);
            visual.localPosition = new Vector3(0f, 1f, 0f);
            CreateVisual(PrimitiveType.Cube, visual, Vector3.zero, new Vector3(0.15f, 0.15f, 0.8f), "Gold");
            CreateVisual(PrimitiveType.Cube, visual, new Vector3(0f, 0f, 0.4f), new Vector3(0.4f, 0.12f, 0.25f), "Gold");

            var light = key.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.3f);
            light.range = 4f;
            light.intensity = 2f;

            var pickup = key.AddComponent<KeyPickup>();
            SetField(pickup, "visual", visual);
            SetField(pickup, "interactRadius", 1.4f);
        }

        private static void CreateDoor(Vector3 pos)
        {
            var door = new GameObject("GuardianDoor");
            door.transform.position = pos;

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "DoorPanel";
            panel.transform.SetParent(door.transform, false);
            panel.transform.localPosition = new Vector3(0f, WallHeight * 0.5f, 0f);
            panel.transform.localScale = new Vector3(Cell * 2f, WallHeight, 0.6f);
            panel.GetComponent<Renderer>().sharedMaterial = Materials["Door"];

            var obstacle = panel.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;

            var locked = door.AddComponent<LockedDoor>();
            SetField(locked, "doorPanel", panel.transform);
            SetField(locked, "interactRadius", 2.8f);
            SetField(locked, "bobAmplitude", 0f);
            SetField(locked, "spinSpeed", 0f);
        }

        private static void CreateArtifact(Vector3 pos)
        {
            var artifact = new GameObject("AncientArtifact");
            artifact.transform.position = pos;

            CreateVisual(PrimitiveType.Cylinder, artifact.transform, new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.4f, 1.2f), "Wall");

            var visual = new GameObject("Visual").transform;
            visual.SetParent(artifact.transform, false);
            visual.localPosition = new Vector3(0f, 1.5f, 0f);
            var gem = CreateVisual(PrimitiveType.Cube, visual, Vector3.zero, Vector3.one * 0.5f, "Artifact");
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);

            var seal = CreateVisual(PrimitiveType.Cylinder, artifact.transform, new Vector3(0f, 0.05f, 0f),
                new Vector3(3f, 0.05f, 3f), "Seal");

            var light = artifact.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.3f, 0.9f, 1f);
            light.range = 6f;
            light.intensity = 3f;

            var pickup = artifact.AddComponent<ArtifactPickup>();
            SetField(pickup, "visual", visual);
            SetField(pickup, "sealVisual", seal);
            SetField(pickup, "interactRadius", 1.6f);
        }

        /// <summary>Gerbang besi antara hall dan ruang timur (dynamic obstacle, menutup saat Key diambil).</summary>
        private static void CreatePortcullis(Vector3 pos)
        {
            var gate = new GameObject("Portcullis");
            gate.transform.position = pos;

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "GatePanel";
            panel.transform.SetParent(gate.transform, false);
            panel.transform.localPosition = new Vector3(0f, WallHeight * 0.5f, 0f);
            panel.transform.localScale = new Vector3(0.5f, WallHeight, Cell * 2f);
            panel.GetComponent<Renderer>().sharedMaterial = Materials["Gate"];

            var obstacle = panel.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;

            var portcullis = gate.AddComponent<Portcullis>();
            SetField(portcullis, "gatePanel", panel.transform);
        }

        // =====================================================
        // UTIL
        // =====================================================

        private static Transform CreatePoint(Transform parent, string pointName, Vector3 pos)
        {
            var go = new GameObject(pointName);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            return go.transform;
        }

        private static GameObject CreateVisual(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale,
            string materialKey)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = materialKey;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (Materials.TryGetValue(materialKey, out Material m))
                go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        private static void CreateMaterials()
        {
            Materials.Clear();
            AddMaterial("Floor", new Color(0.23f, 0.22f, 0.25f));
            AddMaterial("Wall", new Color(0.42f, 0.38f, 0.34f));
            AddMaterial("Door", new Color(0.36f, 0.22f, 0.12f));
            AddMaterial("Player", new Color(0.2f, 0.45f, 0.95f));
            AddMaterial("Visor", new Color(0.9f, 0.95f, 1f), new Color(0.4f, 0.6f, 1f));
            AddMaterial("Sword", new Color(0.85f, 0.88f, 0.92f));
            AddMaterial("Melee", new Color(0.78f, 0.75f, 0.65f));
            AddMaterial("Ranged", new Color(0.55f, 0.6f, 0.45f));
            AddMaterial("Guardian", new Color(0.35f, 0.2f, 0.45f));
            AddMaterial("Seal", new Color(0.6f, 0.2f, 1f), new Color(0.8f, 0.2f, 1.6f));
            AddMaterial("Balcony", new Color(0.33f, 0.3f, 0.28f));
            AddMaterial("TrapPlate", new Color(0.35f, 0.08f, 0.06f));
            AddMaterial("Gate", new Color(0.25f, 0.25f, 0.28f));
            AddMaterial("Eyes", new Color(1f, 0.2f, 0.1f), new Color(2f, 0.2f, 0.1f));
            AddMaterial("Metal", new Color(0.55f, 0.55f, 0.6f));
            AddMaterial("Wood", new Color(0.45f, 0.3f, 0.15f));
            AddMaterial("Gold", new Color(1f, 0.8f, 0.2f), new Color(1.2f, 0.9f, 0.2f));
            AddMaterial("Artifact", new Color(0.3f, 0.9f, 1f), new Color(0.6f, 1.8f, 2.2f));
            AddMaterial("Projectile", new Color(1f, 0.6f, 0.1f), new Color(2f, 1f, 0.2f));
            AddMaterial("Flame", new Color(1f, 0.6f, 0.2f), new Color(3f, 1.4f, 0.3f));
        }

        private static void AddMaterial(string key, Color baseColor, Color? emission = null)
        {
            string path = $"{MaterialDir}/{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            Materials[key] = mat;
        }

        private static void SetField(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
                throw new ArgumentException($"Field '{fieldName}' tidak ditemukan di {target.GetType().Name}");

            switch (value)
            {
                case float f: prop.floatValue = f; break;
                case int i: prop.intValue = i; break;
                case bool b: prop.boolValue = b; break;
                case Object o: prop.objectReferenceValue = o; break;
                default: throw new ArgumentException($"Tipe {value?.GetType()} belum didukung");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
