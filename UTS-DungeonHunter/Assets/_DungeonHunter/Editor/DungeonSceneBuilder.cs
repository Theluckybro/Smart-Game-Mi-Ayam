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
using KK = DungeonHunter.EditorTools.KayKitVisuals;

namespace DungeonHunter.EditorTools
{
    /// <summary>
    /// Membangun scene "Dungeon Hunter" secara otomatis dari peta ASCII.
    /// Menu: Dungeon Hunter → Build Prototype Scene
    ///
    /// Legenda peta (1 karakter = 1 sel 2x2 meter):
    ///   #  dinding        .  lantai          P  spawn player
    ///   M  melee enemy    R  crossbowman     Q  mage          G  guardian
    ///   K  dungeon key    D  pintu guardian  A  artifact      H  health potion
    ///   w  waypoint       S  retreat point   L  obor / point light
    ///   T  jebakan duri (NavMesh area "Trap", cost tinggi; setiap kelompok sel = 1 jebakan)
    ///   g  gerbang Treasury  (tertutup → dibuka tuas V → terbanting lagi saat Key diambil)
    ///   j  gerbang jalan pintas Katakombe → Entrance (dibuka tuas v)
    ///   V  tuas Armory     v  tuas Katakombe
    ///
    /// Ruangan & fungsinya:
    ///   Entrance (spawn) → Great Hall (pusat, patroli + jebakan) → Armory (barat: balkon, tuas V)
    ///   → Treasury (timur: Key, gerbang menutup di belakang player) → Katakombe (tenggara: dua lorong,
    ///   jebakan vs jalan memutar, penyergapan, tuas v) → jalan pintas ke Entrance → Vestibule → Sanctum Guardian.
    ///
    /// Struktur tambahan (didefinisikan lewat konstanta, bukan karakter peta):
    ///   Balkon setinggi 1.4 m di Armory + ramp naik + NavMeshLink untuk lompat turun.
    ///
    /// Visual memakai aset KayKit (CC0) di Assets/_DungeonHunter/ThirdParty/KayKit.
    /// Collider & NavMesh tetap memakai geometri sederhana (kubus) agar navigasi stabil.
    /// </summary>
    public static class DungeonSceneBuilder
    {
        private const float Cell = 2f;
        private const float WallHeight = 2.5f;
        private const float WallCapThickness = 0.06f;

        // Fasad didorong sedikit ke arah lantai supaya tidak sebidang dengan fasad lain di sudut luar
        private const float FacadeOffset = 0.015f;

        // Balkon Armory (baris 11–14, kolom 1–4) + ramp (baris 11–12, kolom 5–6, naik ke arah barat)
        private const int BalconyRowMin = 11, BalconyRowMax = 14, BalconyColMin = 1, BalconyColMax = 4;
        private const int RampRowMin = 11, RampRowMax = 12, RampColMin = 5, RampColMax = 6;
        private const float BalconyHeight = 1.4f;

        private const string Root = "Assets/_DungeonHunter";
        private const string ScenePath = Root + "/Scenes/DungeonHunter_Progress.unity";
        private const string MaterialDir = Root + "/Materials";
        private const string PrefabDir = Root + "/Prefabs";

        private static readonly string[] Map =
        {
            "########################################",
            "#############L............L#############",
            "#############......A.......#############",
            "#############...#......#...#############",
            "#############..............#############",
            "#############......G.......#############",
            "#############...#......#...#############",
            "#############H.............#############",
            "#############.............L#############",
            "###################DD###################",
            "################H......L################",
            "#V........########....########L........#",
            "#.........##L..............L##.......K.#",
            "#..R......##.w...M........w.##.........#",
            "#........L##...#........#...##.........#",
            "#.........##......TTTT......gg....#....#",
            "#.w.....w....S....TTTT....S.gg.........#",
            "#.................TTTT......##.....Q...#",
            "#.........##...#........#...##........S#",
            "#.....M...##.w........M...w.##L........#",
            "#.w.....w.##L..............L########..##",
            "#S.......L#########..###############..##",
            "###################..###############..##",
            "#############L.........L######..TTT..L##",
            "#############...........######S.TTT.w.##",
            "#############...........##v.....####..##",
            "#############....P......jj.w..........##",
            "#############...........jj...M......w.##",
            "#############..........L##L....H########",
            "########################################",
        };

        // Props dekorasi: (model, baris, kolom, ukuran horizontal terpanjang, rotasi Y)
        private static readonly (string model, int row, int col, float size, float yaw)[] Props =
        {
            // Sanctum
            ("rubble_large", 1, 21, 1.8f, 0f),
            ("rubble_half", 8, 15, 1.6f, 40f),
            ("candle_triple", 2, 16, 0.8f, 0f),
            ("candle_triple", 2, 23, 0.8f, 0f),
            // Great Hall
            ("barrel_large", 20, 14, 1.3f, 0f),
            ("crates_stacked", 12, 25, 1.7f, 0f),
            ("table_medium_decorated_A", 20, 25, 1.8f, 0f),
            // Armory
            ("box_stacked", 12, 1, 1.4f, 0f),
            ("crates_stacked", 21, 4, 1.7f, 0f),
            ("box_large", 18, 1, 1.5f, 90f),
            ("barrel_large", 21, 7, 1.3f, 0f),
            // Treasury
            ("chest_gold", 11, 37, 1.5f, 180f),
            ("chest", 11, 33, 1.5f, 180f),
            ("coin_stack_large", 12, 38, 0.9f, 0f),
            ("coin_stack_large", 11, 35, 0.9f, 60f),
            ("trunk_large_A", 14, 31, 1.8f, 90f),
            ("keg", 19, 32, 1.3f, 90f),
            // Katakombe
            ("trunk_large_B", 25, 29, 1.8f, 0f),
            ("trunk_large_B", 28, 28, 1.8f, 0f),
            ("rubble_half", 27, 37, 1.4f, 0f),
            // Entrance
            ("table_medium_decorated_A", 28, 15, 1.8f, 0f),
            ("bed_floor", 24, 21, 2f, 90f),
            ("keg", 28, 20, 1.3f, 90f),
            ("box_stacked", 25, 13, 1.4f, 90f),
        };

        // Dekorasi dinding: (model, baris dinding, kolom dinding, baris lantai, kolom lantai)
        private static readonly (string model, int wr, int wc, int fr, int fc)[] WallDecor =
        {
            ("banner_shield_red", 0, 15, 1, 15),
            ("sword_shield_gold", 0, 19, 1, 19),
            ("banner_shield_red", 0, 24, 1, 24),
            ("banner_red", 9, 17, 10, 17),
            ("banner_red", 9, 22, 10, 22),
            ("banner_patternA_red", 11, 14, 12, 14),
            ("banner_patternA_red", 11, 25, 12, 25),
            ("sword_shield", 17, 0, 17, 1),
            ("sword_shield_broken", 19, 0, 19, 1),
            ("banner_shield_red", 10, 34, 11, 34),
            ("banner_red", 29, 18, 28, 18),
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
            KK.Init();
            VFXAssets.Create();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();

            // ---------- LEVEL GEOMETRY (collider + NavMesh) ----------
            var level = new GameObject("Level");
            BuildFloor(level.transform);
            BuildWalls(level.transform);
            BuildBalcony(level.transform);
            BuildTrapVolumes(level.transform);
            BuildTorches(level.transform);
            BuildProps(level.transform);

            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            SaveNavMeshData(surface);

            // ---------- VISUAL DUNGEON (KayKit, tanpa collider) ----------
            var visuals = new GameObject("Dungeon Visuals").transform;
            DressFloor(visuals);
            DressWalls(visuals);
            DressWallDecor(visuals);

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
            managers.AddComponent<DungeonHunter.UI.GameUI>();
            managers.AddComponent<AIDebugManager>();
            managers.AddComponent<AudioManager>();
            managers.AddComponent<MusicDirector>();
            var vfx = managers.AddComponent<VFXManager>();
            SetField(vfx, "additive", VFXAssets.Additive);
            SetField(vfx, "alphaBlend", VFXAssets.AlphaBlend);
            SetField(vfx, "ring", VFXAssets.Ring);
            SetField(vfx, "telegraph", VFXAssets.Telegraph);
            SetField(vfx, "debris", VFXAssets.Debris);
            SetField(vfx, "line", VFXAssets.Line);
            SetField(vfx, "solid", VFXAssets.Solid);

            CreateCamera(player.transform);

            // ---------- ENEMIES ----------
            var enemies = new GameObject("Enemies").transform;
            Projectile magicProjectile = CreateMagicProjectilePrefab();
            Projectile arrowProjectile = CreateArrowProjectilePrefab();

            // Setiap waypoint menjadi milik melee terdekat → satu rute patroli per ruangan
            List<Vector3> meleeSpawns = CellsOf('M').ToList();
            int meleeIndex = 0;
            foreach (Vector3 p in meleeSpawns)
            {
                var e = CreateMelee(enemies, p, meleeIndex + 1);
                List<Transform> route = OrderLoop(waypoints
                    .Where(w => meleeSpawns.OrderBy(m => Vector3.Distance(m, w.position)).First() == p).ToList());
                e.Setup($"Skeleton Warrior {meleeIndex + 1}", player.transform, route, retreats);
                e.SetStartWaypoint(meleeIndex % Mathf.Max(1, route.Count));
                meleeIndex++;
            }

            int rangedIndex = 0;
            foreach (Vector3 p in CellsOf('R'))
            {
                var e = CreateRanged(enemies, p, ++rangedIndex, true, arrowProjectile);
                e.Setup("Skeleton Crossbowman", player.transform, new List<Transform>(), retreats);
            }
            foreach (Vector3 p in CellsOf('Q'))
            {
                var e = CreateRanged(enemies, p, ++rangedIndex, false, magicProjectile);
                e.Setup("Skeleton Mage", player.transform, new List<Transform>(), retreats);
            }

            foreach (Vector3 p in CellsOf('G'))
            {
                var g = CreateGuardian(enemies, p);
                g.Setup("Crypt Guardian", player.transform, new List<Transform>(), new List<Transform>());
                SetField(g, "minionPrefab", CreateMinionPrefab());
            }

            // ---------- OBJECTIVES ----------
            CreateKey(CellsOf('K').First());
            CreateDoor(Centroid(CellsOf('D')));
            CreateArtifact(CellsOf('A').First());
            Portcullis treasuryGate = CreatePortcullis('g', "TreasuryGate", true);
            Portcullis shortcutGate = CreatePortcullis('j', "ShortcutGate", false);
            CreateLever(CellsOf('V').First(), "Lever_Armory", 0, treasuryGate);
            CreateLever(CellsOf('v').First(), "Lever_Catacombs", 1, shortcutGate);
            foreach (Vector3 p in CellsOf('H'))
                CreatePotion(p);

            // ---------- POST-PROCESSING ----------
            var post = new GameObject("PostFX");
            var volume = post.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = PostFXAssets.Create();
            post.AddComponent<PostFXController>();

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

        private static bool OnRamp(int row, int col) =>
            row >= RampRowMin && row <= RampRowMax && col >= RampColMin && col <= RampColMax;

        private static bool IsWall(int row, int col) =>
            row < 0 || row >= Rows || col < 0 || col >= Cols || Map[row][col] == '#';

        private static float GroundHeight(int row, int col) => OnBalcony(row, col) ? BalconyHeight : 0f;

        /// <summary>Posisi dunia sebuah sel, termasuk ketinggian lantai (balkon = 1.4 m).</summary>
        private static IEnumerable<Vector3> CellsOf(char c)
        {
            for (int r = 0; r < Rows; r++)
                for (int col = 0; col < Cols; col++)
                    if (Map[r][col] == c)
                        yield return CellToWorld(r, col) + Vector3.up * GroundHeight(r, col);
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
        // LEVEL (collider + NavMesh)
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

        /// <summary>
        /// Dinding = kubus collider per run horizontal. Renderer-nya hanya terlihat sebagai "atap" gelap;
        /// sisi yang menghadap lantai ditutup fasad KayKit (DressWalls).
        /// </summary>
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
                    wall.isStatic = true;

                    // Kubus hanya collider. Kalau ikut dirender, sisinya sejajar persis dengan fasad KayKit
                    // dan menimbulkan z-fighting (kedip). Yang terlihat dari atas cukup "tutup" tipis di atasnya.
                    wall.GetComponent<Renderer>().enabled = false;

                    // Pilar tunggal (dikelilingi lantai) memakai model pilar KayKit, tanpa tutup
                    if (!(length == 1 && IsIsolatedPillar(r, start)))
                    {
                        GameObject cap = CreateVisual(PrimitiveType.Cube, walls, Vector3.zero,
                            new Vector3(length * Cell, WallCapThickness, Cell), "WallTop");
                        cap.name = $"WallCap_{r}_{start}";
                        cap.transform.position = (a + b) * 0.5f + Vector3.up * (WallHeight + WallCapThickness * 0.5f);
                        cap.isStatic = true;
                    }

                    var modifier = wall.AddComponent<NavMeshModifier>();
                    modifier.overrideArea = true;
                    modifier.area = 1; // Not Walkable
                }
            }
        }

        private static bool IsIsolatedPillar(int r, int c) =>
            !IsWall(r - 1, c) && !IsWall(r + 1, c) && !IsWall(r, c - 1) && !IsWall(r, c + 1);

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
            ramp.GetComponent<Renderer>().sharedMaterial = Materials["Wood"];
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

        /// <summary>Kelompokkan sel 'T' yang bersebelahan (flood fill); setiap kelompok = satu persegi panjang (x, z).</summary>
        private static IEnumerable<Rect> TrapRects()
        {
            var seen = new HashSet<(int, int)>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    if (Map[r][c] != 'T' || seen.Contains((r, c)))
                        continue;
                    var group = new List<(int r, int c)>();
                    var stack = new Stack<(int, int)>();
                    stack.Push((r, c));
                    seen.Add((r, c));
                    while (stack.Count > 0)
                    {
                        var (cr, cc) = stack.Pop();
                        group.Add((cr, cc));
                        foreach (var (dr, dc) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int nr = cr + dr, nc = cc + dc;
                            if (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols && Map[nr][nc] == 'T' && seen.Add((nr, nc)))
                                stack.Push((nr, nc));
                        }
                    }
                    List<Vector3> cells = group.Select(g => CellToWorld(g.r, g.c)).ToList();
                    float minX = cells.Min(p => p.x) - Cell * 0.5f, maxX = cells.Max(p => p.x) + Cell * 0.5f;
                    float minZ = cells.Min(p => p.z) - Cell * 0.5f, maxZ = cells.Max(p => p.z) + Cell * 0.5f;
                    yield return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
                }
        }

        /// <summary>
        /// Obor: tiang (KayKit column) + api + point light. Tiang punya collider kecil.
        /// Di sel sempit (koridor) hanya api + cahaya, supaya jalur NavMesh tidak tertutup.
        /// </summary>
        private static void BuildTorches(Transform parent)
        {
            var torches = new GameObject("Torches").transform;
            torches.SetParent(parent);
            int i = 0;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    if (Map[r][c] != 'L')
                        continue;

                    Vector3 p = CellToWorld(r, c) + Vector3.up * GroundHeight(r, c);
                    var go = new GameObject($"Torch_{i++}");
                    go.transform.SetParent(torches);
                    go.transform.position = p;

                    bool narrow = IsWall(r - 1, c) || IsWall(r + 1, c) || IsWall(r, c - 1) || IsWall(r, c + 1);
                    if (!narrow)
                    {
                        KK.Place("column", go.transform, p, Quaternion.identity, new Vector3(0.6f, 1.5f, 0f));
                        var col = go.AddComponent<CapsuleCollider>();
                        col.center = new Vector3(0f, 0.75f, 0f);
                        col.height = 1.5f;
                        col.radius = 0.35f;
                    }

                    var flame = CreateVisual(PrimitiveType.Sphere, go.transform, new Vector3(0f, narrow ? 2.3f : 1.7f, 0f), Vector3.one * 0.3f, "Flame");
                    flame.name = "Flame";

                    var lightGo = new GameObject("Light");
                    lightGo.transform.SetParent(go.transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.6f, 0.25f);
                    light.range = 11f;
                    light.intensity = 4.5f;

                    var torchFx = go.AddComponent<TorchFX>();
                    SetField(torchFx, "torchLight", light);
                    SetField(torchFx, "flame", flame.transform);
                }
        }

        /// <summary>Props dekorasi dengan BoxCollider (ikut di-bake sehingga enemy berjalan memutarinya).</summary>
        private static void BuildProps(Transform parent)
        {
            var props = new GameObject("Props").transform;
            props.SetParent(parent);
            foreach (var p in Props)
            {
                Vector3 pos = CellToWorld(p.row, p.col) + Vector3.up * GroundHeight(p.row, p.col);
                var go = new GameObject(p.model);
                go.transform.SetParent(props, false);
                KK.Spawn(KK.DungeonPath(p.model), go.transform);

                Bounds b = KK.RendererBounds(go);
                go.transform.localScale = Vector3.one * (p.size / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)));
                go.transform.rotation = Quaternion.Euler(0f, p.yaw, 0f);
                b = KK.RendererBounds(go);
                go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);

                // Collider dihitung dari bounds model dalam ruang lokal container
                Quaternion rot = go.transform.rotation;
                go.transform.rotation = Quaternion.identity;
                b = KK.RendererBounds(go);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(b.center);
                box.size = b.size / go.transform.localScale.x;
                go.transform.rotation = rot;
            }
        }

        private static void SaveNavMeshData(NavMeshSurface surface)
        {
            string path = Root + "/Scenes/DungeonHunter_NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
        }

        // =====================================================
        // VISUAL DUNGEON (KayKit)
        // =====================================================

        /// <summary>Ubin lantai KayKit per sel (2x2 m). Sel jebakan memakai grate (dibuat di BuildSpikeTraps).</summary>
        private static void DressFloor(Transform parent)
        {
            var floor = new GameObject("Floor Tiles").transform;
            floor.SetParent(parent);
            var rng = new System.Random(7);

            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    char ch = Map[r][c];
                    if (ch == '#' || ch == 'T' || OnRamp(r, c))
                        continue;

                    double roll = rng.NextDouble();
                    string model = roll < 0.05 ? "floor_tile_large_rocks" : roll < 0.09 ? "floor_tile_small_broken_A" : "floor_tile_large";
                    Vector3 pos = CellToWorld(r, c) + Vector3.up * (GroundHeight(r, c) - 0.05f);
                    var tile = KK.Place(model, floor, pos, Quaternion.Euler(0f, 90f * rng.Next(4), 0f), new Vector3(Cell, 0f, Cell));
                    tile.isStatic = true;
                }
        }

        /// <summary>
        /// Fasad dinding KayKit di setiap sisi sel dinding yang berbatasan dengan lantai.
        /// Pilar tunggal memakai model pilar.
        /// </summary>
        private static void DressWalls(Transform parent)
        {
            var walls = new GameObject("Wall Facades").transform;
            walls.SetParent(parent);
            var rng = new System.Random(11);
            (int dr, int dc)[] dirs = { (-1, 0), (1, 0), (0, -1), (0, 1) };

            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    if (!IsWall(r, c))
                        continue;

                    Vector3 center = CellToWorld(r, c);
                    if (IsIsolatedPillar(r, c))
                    {
                        var pillar = KK.Place("pillar", walls, center, Quaternion.identity, new Vector3(Cell * 0.9f, WallHeight, Cell * 0.9f));
                        pillar.isStatic = true;
                        continue;
                    }

                    foreach (var (dr, dc) in dirs)
                    {
                        int fr = r + dr, fc = c + dc;
                        if (IsWall(fr, fc))
                            continue;

                        // arah dari dinding ke lantai (dunia): baris naik = z turun
                        Vector3 toFloor = new Vector3(dc, 0f, -dr);
                        Vector3 boundary = center + toFloor * (Cell * 0.5f);
                        string model = rng.NextDouble() < 0.15 ? "wall_cracked" : "wall";
                        var facade = KK.Place(model, walls, boundary - toFloor * (0.25f - FacadeOffset),
                            Quaternion.LookRotation(toFloor), new Vector3(Cell, WallHeight, 0.5f));
                        facade.isStatic = true;
                    }
                }
        }

        /// <summary>Banner dan perisai di dinding (ikut skala fasad 0.5 agar proporsinya sama).</summary>
        private static void DressWallDecor(Transform parent)
        {
            var decor = new GameObject("Wall Decor").transform;
            decor.SetParent(parent);
            foreach (var d in WallDecor)
            {
                Vector3 toFloor = new Vector3(d.fc - d.wc, 0f, -(d.fr - d.wr));
                Vector3 boundary = CellToWorld(d.wr, d.wc) + toFloor * (Cell * 0.5f);
                var go = new GameObject(d.model);
                go.transform.SetParent(decor, false);
                KK.Spawn(KK.DungeonPath(d.model), go.transform);
                go.transform.rotation = Quaternion.LookRotation(toFloor);
                go.transform.localScale = new Vector3(0.5f, 0.55f, 0.5f);
                go.transform.position = boundary - toFloor * 0.25f;
            }
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

        /// <summary>
        /// Jebakan duri: grate KayKit sebagai lantai + floor_tile_big_spikes sebagai duri yang naik-turun.
        /// </summary>
        private static void BuildSpikeTraps()
        {
            foreach (Rect r in TrapRects())
            {
                var trap = new GameObject("SpikeTrap");
                trap.transform.position = new Vector3(r.center.x, 0f, r.center.y);

                var spikes = new GameObject("Spikes").transform;
                spikes.SetParent(trap.transform, false);

                for (float x = r.xMin + Cell * 0.5f; x < r.xMax; x += Cell)
                    for (float z = r.yMin + Cell * 0.5f; z < r.yMax; z += Cell)
                    {
                        Vector3 p = new Vector3(x, 0f, z);
                        KK.Place("floor_tile_big_grate", trap.transform, p + Vector3.down * 0.05f, Quaternion.identity, new Vector3(Cell, 0f, Cell));
                        KK.Place("floor_tile_big_spikes", spikes, p, Quaternion.identity, new Vector3(Cell * 0.9f, 0f, Cell * 0.9f));
                    }

                // Duri disembunyikan di bawah lantai; SpikeTrap menaikkannya saat aktif
                Bounds sb = KK.RendererBounds(spikes.gameObject);
                spikes.localPosition = new Vector3(0f, -sb.size.y - 0.02f, 0f);

                var spikeTrap = trap.AddComponent<SpikeTrap>();
                var so = new SerializedObject(spikeTrap);
                so.FindProperty("size").vector3Value = new Vector3(r.width, 1f, r.height);
                so.FindProperty("spikes").objectReferenceValue = spikes;
                so.FindProperty("spikeUpHeight").floatValue = sb.size.y * 0.85f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetupLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.28f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.07f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 48f;

            var sun = new GameObject("Moonlight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.6f, 0.65f, 0.9f);
            sun.intensity = 0.55f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
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

            KK.AttachCharacter(player, new KK.CharacterSpec
            {
                Model = "Knight", Material = "Knight", Controller = "Player", Height = 2.1f,
                RightWeapon = "sword_1handed", RightWeaponSize = 0.62f,
                LeftWeapon = "shield_badge", LeftWeaponSize = 0.4f
            });

            player.AddComponent<HitFlash>();
            AddDamageFeedback(player, true, false, new Color(1f, 0.45f, 0.3f), new Color(0.6f, 0.6f, 0.68f), 7f, 1.2f);
            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerCombat>();
            AddFootsteps(player, Sfx.PlayerStep, 1.9f, 1f, 5f);
            player.AddComponent<ObjectivePointer>();
            return player;
        }

        private static void CreateCamera(Transform player)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.02f, 0.05f);
            cam.fieldOfView = 50f;
            // AudioListener tidak di kamera: AudioManager membuat listener yang mengikuti player

            // Post-processing URP aktif + anti-aliasing SMAA
            var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;

            var follow = camGo.AddComponent<TopDownCamera>();
            follow.SetTarget(player);
            SetField(follow, "menuCenter", new Vector3((Cols - 1) * Cell * 0.5f, 0f, (Rows - 1) * Cell * 0.5f));
            SetField(follow, "menuRadius", Cols * Cell * 0.42f);
            SetField(follow, "menuHeight", Cols * Cell * 0.42f);
            Vector3 offset = new Vector3(0f, 16f, -9f);
            camGo.transform.position = player.position + offset;
            camGo.transform.rotation = Quaternion.LookRotation(-offset);
        }

        private static T CreateEnemyBase<T>(Transform parent, string objName, Vector3 pos, float radius, float height,
            float maxHp, KK.CharacterSpec model) where T : EnemyAI
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

            KK.AttachCharacter(go, model);

            go.AddComponent<HitFlash>();
            go.AddComponent<AIPerception>();
            go.AddComponent<AIMemory>();
            return go.AddComponent<T>();
        }

        private static MeleeEnemy CreateMelee(Transform parent, Vector3 pos, int index)
        {
            var e = CreateEnemyBase<MeleeEnemy>(parent, $"Melee_{index}", pos, 0.45f, 2f, 60f, new KK.CharacterSpec
            {
                Model = "Skeleton_Warrior", Material = "Skeleton", Controller = "SkeletonMelee", Height = 1.95f,
                RightWeapon = "Skeleton_Blade", RightWeaponSize = 0.62f,
                LeftWeapon = "Skeleton_Shield_Small_A", LeftWeaponSize = 0.38f
            });
            e.GetComponent<AIPerception>().Configure(9f, 110f);
            AddDamageFeedback(e.gameObject, false, false, BoneSpark, BoneDebris, 6f, 1.1f);
            SetField(e, "alertHeight", 2.6f);
            AddFootsteps(e.gameObject, Sfx.SkeletonStep, 1.3f, 1f, 3.5f);
            return e;
        }

        private static RangedEnemy CreateRanged(Transform parent, Vector3 pos, int index, bool crossbow, Projectile prefab)
        {
            KK.CharacterSpec spec = crossbow
                ? new KK.CharacterSpec
                {
                    Model = "Skeleton_Rogue", Material = "Skeleton", Controller = "SkeletonCrossbow", Height = 1.9f,
                    RightWeapon = "Skeleton_Crossbow", RightWeaponSize = 0.6f
                }
                : new KK.CharacterSpec
                {
                    Model = "Skeleton_Mage", Material = "Skeleton", Controller = "SkeletonMage", Height = 2f,
                    RightWeapon = "Skeleton_Staff", RightWeaponSize = 0.95f
                };

            var e = CreateEnemyBase<RangedEnemy>(parent, $"Ranged_{index}", pos, 0.45f, 2f, 45f, spec);
            e.GetComponent<AIPerception>().Configure(12f, 100f);
            SetField(e, "trapAreaCost", 8f);
            SetField(e, "fxColor", crossbow ? ArrowColor : MagicColor);
            SetField(e, "magic", !crossbow);
            AddDamageFeedback(e.gameObject, false, false, BoneSpark, BoneDebris, 6f, 1.1f);
            SetField(e, "alertHeight", 2.6f);
            AddFootsteps(e.gameObject, Sfx.SkeletonStep, 1.3f, 1f, 3.5f);

            var muzzle = new GameObject("FirePoint").transform;
            muzzle.SetParent(e.transform, false);
            muzzle.localPosition = new Vector3(0f, 1.3f, 0.7f);
            e.SetProjectile(prefab, muzzle);
            return e;
        }

        /// <summary>
        /// Prefab Skeleton Minion untuk Phase 2 Guardian: kecil, cepat, rapuh (HP 25), tidak kabur (Flee off).
        /// Di-Instantiate saat runtime oleh GuardianEnemy.
        /// </summary>
        private static MeleeEnemy CreateMinionPrefab()
        {
            var e = CreateEnemyBase<MeleeEnemy>(null, "Skeleton_Minion", Vector3.zero, 0.38f, 1.6f, 25f, new KK.CharacterSpec
            {
                Model = "Skeleton_Minion", Material = "Skeleton", Controller = "SkeletonMelee", Height = 1.55f,
                RightWeapon = "Skeleton_Blade", RightWeaponSize = 0.5f
            });
            e.GetComponent<AIPerception>().Configure(14f, 160f);
            SetField(e, "canFlee", false);
            SetField(e, "chaseSpeed", 4.6f);
            SetField(e, "attackDamage", 8f);
            SetField(e, "attackCooldown", 1.4f);
            SetField(e, "alertHeight", 2.1f);
            AddDamageFeedback(e.gameObject, false, false, BoneSpark, BoneDebris, 7f, 0.9f);
            AddFootsteps(e.gameObject, Sfx.SkeletonStep, 1.1f, 0.8f, 3.5f);

            string path = PrefabDir + "/Skeleton_Minion.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(e.gameObject, path);
            Object.DestroyImmediate(e.gameObject);
            return prefab.GetComponent<MeleeEnemy>();
        }

        private static GuardianEnemy CreateGuardian(Transform parent, Vector3 pos)
        {
            var g = CreateEnemyBase<GuardianEnemy>(parent, "Guardian", pos, 0.8f, 3f, 400f, new KK.CharacterSpec
            {
                Model = "Skeleton_Warrior", Material = "SkeletonGuardian", Controller = "Guardian", Height = 3.1f,
                RightWeapon = "Skeleton_Axe", RightWeaponSize = 0.62f,
                LeftWeapon = "Skeleton_Shield_Large_B", LeftWeaponSize = 0.45f,
                ReactToHits = false
            });
            // boss mengawasi seluruh arena (28×16 m); pilar tetap memblokir line of sight
            g.GetComponent<AIPerception>().Configure(20f, 200f);
            SetField(g, "chaseSpeed", 3.2f);
            SetField(g, "patrolSpeed", 1.8f);
            SetField(g, "trapAreaCost", 1f);
            SetField(g, "knockbackScale", 0.08f);
            SetField(g, "alertHeight", 4f);
            AddDamageFeedback(g.gameObject, false, true, new Color(0.85f, 0.6f, 1f), new Color(0.6f, 0.45f, 0.75f), 6f, 1.8f);
            AddFootsteps(g.gameObject, Sfx.GuardianStep, 2.2f, 1f, 3f);
            return g;
        }

        private static Projectile CreateMagicProjectilePrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile_Magic";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 0.3f;
            go.GetComponent<Renderer>().sharedMaterial = Materials["Projectile"];
            var projectile = go.AddComponent<Projectile>();
            SetField(projectile, "fxColor", MagicColor);
            SetField(projectile, "magic", true);
            AddProjectileLight(go, MagicColor);
            AddTrail(go, MagicColor, 0.35f, 0.3f);
            return SavePrefab(go, "Projectile_Magic");
        }

        private static Projectile CreateArrowProjectilePrefab()
        {
            var go = new GameObject("Projectile_Arrow");
            var holder = new GameObject("Model").transform;
            holder.SetParent(go.transform, false);
            KK.Spawn(KK.WeaponPath("Skeleton_Arrow"), holder, "Skeleton");

            // Putar model agar sumbu terpanjangnya searah +Z (arah terbang proyektil)
            Bounds b = KK.RendererBounds(holder.gameObject);
            if (b.size.y >= b.size.x && b.size.y >= b.size.z)
                holder.localRotation = Quaternion.Euler(90f, 0f, 0f);
            else if (b.size.x >= b.size.z)
                holder.localRotation = Quaternion.Euler(0f, 90f, 0f);

            b = KK.RendererBounds(holder.gameObject);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            holder.localScale = Vector3.one * (0.8f / Mathf.Max(0.01f, longest));
            b = KK.RendererBounds(holder.gameObject);
            holder.localPosition -= b.center - go.transform.position;

            var projectile = go.AddComponent<Projectile>();
            SetField(projectile, "fxColor", ArrowColor);
            SetField(projectile, "magic", false);
            AddProjectileLight(go, ArrowColor);
            AddTrail(go, ArrowColor, 0.15f, 0.22f);
            return SavePrefab(go, "Projectile_Arrow");
        }

        private static readonly Color MagicColor = new Color(0.75f, 0.4f, 1f);
        private static readonly Color ArrowColor = new Color(1f, 0.65f, 0.3f);
        private static readonly Color BoneSpark = new Color(1f, 0.92f, 0.65f);
        private static readonly Color BoneDebris = new Color(0.92f, 0.88f, 0.78f);

        private static void AddDamageFeedback(GameObject go, bool isPlayer, bool isBoss, Color spark, Color debris,
            float knockback, float bodyHeight)
        {
            var fb = go.AddComponent<DamageFeedback>();
            SetField(fb, "isPlayer", isPlayer);
            SetField(fb, "isBoss", isBoss);
            SetField(fb, "sparkColor", spark);
            SetField(fb, "debrisColor", debris);
            SetField(fb, "knockback", knockback);
            SetField(fb, "bodyHeight", bodyHeight);
        }

        /// <summary>Jejak cahaya di belakang proyektil.</summary>
        private static void AddTrail(GameObject go, Color color, float width, float time)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = VFXAssets.Trail;
            trail.time = time;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.25f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private static void AddProjectileLight(GameObject go, Color color)
        {
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 3f;
            light.intensity = 2f;
        }

        private static Projectile SavePrefab(GameObject go, string name)
        {
            string path = PrefabDir + "/" + name + ".prefab";
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
            GameObject model = KK.Spawn(KK.DungeonPath("key"), visual);
            Bounds b = KK.RendererBounds(model);
            model.transform.localScale *= 1.2f / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            b = KK.RendererBounds(model);
            model.transform.position -= b.center - visual.position;

            var light = key.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.3f);
            light.range = 4f;
            light.intensity = 2.5f;

            var pickup = key.AddComponent<KeyPickup>();
            SetField(pickup, "visual", visual);
            SetField(pickup, "interactRadius", 1.4f);

            var sparkles = key.AddComponent<AmbientSparkles>();
            SetField(sparkles, "color", new Color(1f, 0.85f, 0.3f));
            SetField(sparkles, "height", 1f);
            SetField(sparkles, "radius", 0.6f);
        }

        /// <summary>Health potion: botol KayKit berwarna merah, cahaya merah, kilau, melayang & berputar.</summary>
        private static void CreatePotion(Vector3 pos)
        {
            var potion = new GameObject("HealthPotion");
            potion.transform.position = pos;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(potion.transform, false);
            visual.localPosition = new Vector3(0f, 0.8f, 0f);
            GameObject model = KK.Spawn(KK.DungeonPath("bottle_A_brown"), visual, "Potion");
            Bounds b = KK.RendererBounds(model);
            model.transform.localScale *= 1.05f / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            b = KK.RendererBounds(model);
            model.transform.position -= b.center - visual.position;

            var light = potion.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.3f, 0.35f);
            light.range = 4f;
            light.intensity = 2.6f;

            var pickup = potion.AddComponent<HealthPotion>();
            SetField(pickup, "visual", visual);
            SetField(pickup, "interactRadius", 1.3f);
            SetField(pickup, "spinSpeed", 60f);

            var sparkles = potion.AddComponent<AmbientSparkles>();
            SetField(sparkles, "color", new Color(1f, 0.35f, 0.4f));
            SetField(sparkles, "height", 0.8f);
            SetField(sparkles, "radius", 0.45f);
        }

        /// <summary>
        /// Panel bergerak = transform kosong berisi "Blocker" (kubus collider + NavMeshObstacle, tidak dirender)
        /// dan model gerbang KayKit.
        /// </summary>
        private static Transform CreateGatePanel(Transform parent, Vector3 blockerScale, float yaw)
        {
            var panel = new GameObject("Panel").transform;
            panel.SetParent(parent, false);

            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Blocker";
            blocker.transform.SetParent(panel, false);
            blocker.transform.localPosition = new Vector3(0f, WallHeight * 0.5f, 0f);
            blocker.transform.localScale = blockerScale;
            blocker.GetComponent<Renderer>().enabled = false;

            var obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;

            // Sedikit lebih sempit dari celah agar ujungnya tidak sebidang dengan fasad di kiri-kanan
            KK.Place("wall_gated", panel, parent.position, Quaternion.Euler(0f, yaw, 0f), new Vector3(Cell * 2f - 0.12f, WallHeight - 0.02f, 0.5f));
            return panel;
        }

        private static void CreateDoor(Vector3 pos)
        {
            var door = new GameObject("GuardianDoor");
            door.transform.position = pos;
            Transform panel = CreateGatePanel(door.transform, new Vector3(Cell * 2f, WallHeight, 0.6f), 0f);

            var locked = door.AddComponent<LockedDoor>();
            SetField(locked, "doorPanel", panel);
            SetField(locked, "interactRadius", 2.8f);
            SetField(locked, "bobAmplitude", 0f);
            SetField(locked, "spinSpeed", 0f);
        }

        private static void CreateArtifact(Vector3 pos)
        {
            var artifact = new GameObject("AncientArtifact");
            artifact.transform.position = pos;

            KK.Place("column", artifact.transform, pos, Quaternion.identity, new Vector3(0.9f, 1f, 0f));

            var visual = new GameObject("Visual").transform;
            visual.SetParent(artifact.transform, false);
            visual.localPosition = new Vector3(0f, 1.6f, 0f);
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

            var sparkles = artifact.AddComponent<AmbientSparkles>();
            SetField(sparkles, "color", new Color(0.4f, 0.95f, 1f));
            SetField(sparkles, "height", 1.6f);
            SetField(sparkles, "radius", 0.9f);
            SetField(sparkles, "rate", 16f);
        }

        /// <summary>
        /// Gerbang besi (dynamic obstacle) dari sekelompok sel peta. Orientasi otomatis:
        /// ada dinding di utara gerbang → lorong timur-barat → panel melintang utara-selatan.
        /// </summary>
        private static Portcullis CreatePortcullis(char cell, string objName, bool closeOnKey)
        {
            List<(int r, int c)> cells = new List<(int, int)>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (Map[r][c] == cell)
                        cells.Add((r, c));
            int rMin = cells.Min(x => x.r), rMax = cells.Max(x => x.r);
            int cMin = cells.Min(x => x.c), cMax = cells.Max(x => x.c);
            bool eastWest = IsWall(rMin - 1, cMin);

            var gate = new GameObject(objName);
            gate.transform.position = Centroid(cells.Select(x => CellToWorld(x.r, x.c)));
            Transform panel = eastWest
                ? CreateGatePanel(gate.transform, new Vector3(0.5f, WallHeight, (rMax - rMin + 1) * Cell), 90f)
                : CreateGatePanel(gate.transform, new Vector3((cMax - cMin + 1) * Cell, WallHeight, 0.5f), 0f);

            var portcullis = gate.AddComponent<Portcullis>();
            SetField(portcullis, "gatePanel", panel);
            SetField(portcullis, "startClosed", true);
            SetField(portcullis, "closeOnKey", closeOnKey);
            return portcullis;
        }

        /// <summary>Tuas: alas batu + gagang kayu yang berayun + lampu indikator (kuning → hijau saat ditarik).</summary>
        private static void CreateLever(Vector3 pos, string objName, int index, Portcullis gate)
        {
            var lever = new GameObject(objName);
            lever.transform.position = pos;

            CreateVisual(PrimitiveType.Cube, lever.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.6f), "WallTop");
            CreateVisual(PrimitiveType.Cube, lever.transform, new Vector3(0f, 0.52f, 0f), new Vector3(0.7f, 0.06f, 0.4f), "Balcony");
            var pivot = new GameObject("Handle").transform;
            pivot.SetParent(lever.transform, false);
            pivot.localPosition = new Vector3(0f, 0.55f, 0f);
            pivot.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            CreateVisual(PrimitiveType.Cylinder, pivot, new Vector3(0f, 0.45f, 0f), new Vector3(0.1f, 0.45f, 0.1f), "Wood");
            CreateVisual(PrimitiveType.Sphere, pivot, new Vector3(0f, 0.92f, 0f), Vector3.one * 0.22f, "Flame");

            var col = lever.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.25f, 0f);
            col.size = new Vector3(0.9f, 0.5f, 0.6f);

            var lampGo = new GameObject("Lamp");
            lampGo.transform.SetParent(lever.transform, false);
            lampGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 4f;
            lamp.intensity = 2.5f;

            var l = lever.AddComponent<Lever>();
            SetField(l, "gate", gate);
            SetField(l, "leverIndex", index);
            SetField(l, "handle", pivot);
            SetField(l, "lamp", lamp);
            SetField(l, "interactRadius", 1.6f);
            SetField(l, "bobAmplitude", 0f);
            SetField(l, "spinSpeed", 0f);

            var sparkles = lever.AddComponent<AmbientSparkles>();
            SetField(sparkles, "color", new Color(1f, 0.8f, 0.3f));
            SetField(sparkles, "height", 1.2f);
            SetField(sparkles, "radius", 0.6f);
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
            AddMaterial("Floor", new Color(0.12f, 0.11f, 0.13f));
            AddMaterial("WallTop", new Color(0.16f, 0.15f, 0.17f));
            AddMaterial("Balcony", new Color(0.33f, 0.3f, 0.28f));
            AddMaterial("Wood", new Color(0.45f, 0.3f, 0.15f));
            AddMaterial("Artifact", new Color(0.3f, 0.9f, 1f), new Color(0.6f, 1.8f, 2.2f));
            AddMaterial("Seal", new Color(0.6f, 0.2f, 1f), new Color(0.8f, 0.2f, 1.6f));
            AddMaterial("Projectile", new Color(0.7f, 0.4f, 1f), new Color(1.6f, 0.8f, 2.4f));
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

        private static void AddFootsteps(GameObject go, Sfx sfx, float stride, float volume, float referenceSpeed)
        {
            var steps = go.AddComponent<FootstepAudio>();
            SetField(steps, "sfx", (int)sfx);
            SetField(steps, "strideLength", stride);
            SetField(steps, "volume", volume);
            SetField(steps, "referenceSpeed", referenceSpeed);
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
                case Color col: prop.colorValue = col; break;
                case Vector3 v: prop.vector3Value = v; break;
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
