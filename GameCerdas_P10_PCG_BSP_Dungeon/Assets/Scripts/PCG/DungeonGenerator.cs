using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
[Header("Dungeon Size")]
[SerializeField] private int mapWidth = 60;
[SerializeField] private int mapHeight = 40;
[SerializeField] private float tileSize = 1f;
[SerializeField] private GameObject cameraScene;

[Header("BSP Settings")]
[SerializeField] private int minLeafSize = 12;
[SerializeField] private int maxLeafSize = 20;

[Header("Room Settings")]
[SerializeField] private int minRoomSize = 5;
[SerializeField] private int roomMargin = 2;

[Header("Corridor Settings")]
[Range(1, 3)]
[SerializeField] private int corridorWidth = 1;

[Header("Generation")]
[SerializeField] private int seed = 12345;
[SerializeField] private bool randomSeed = false;

[Header("Validation")]
[SerializeField] private int maxGenerateAttempts = 10;

[Header("Prefabs")]
[SerializeField] private GameObject floorPrefab;
[SerializeField] private GameObject wallPrefab;
[SerializeField] private GameObject goalPrefab;
[SerializeField] private GameObject playerPrefab;
[SerializeField] private GameObject enemyPrefab;
[SerializeField] private GameObject itemPrefab;

[Header("Spawn Settings")]
[SerializeField] private int enemyCount = 8;
[SerializeField] private int itemCount = 5;
[SerializeField] private int safeRoomCount = 1;
[SerializeField] private float safeRadius = 8f;
[SerializeField] private int maxPlacementAttempts = 100;

private TileType[,] grid;

private BSPNode rootNode;

private List<BSPNode> leafNodes = new List<BSPNode>();
private List<DungeonRoom> rooms = new List<DungeonRoom>();

private Transform levelRoot;

private Vector2Int startPosition;
private Vector2Int goalPosition;

// Posisi (grid) enemy dan item yang berhasil ditempatkan
private List<Vector2Int> enemyPositions = new List<Vector2Int>();
private List<Vector2Int> itemPositions = new List<Vector2Int>();

// Hasil generasi terakhir (dipakai log dan HUD)
private int attemptsUsed;
private bool dungeonValid;
private string statusMessage = "";
private List<Vector2Int> currentPath;

// Riwayat seed untuk tombol Prev Seed (T)
private List<int> seedHistory = new List<int>();

private void Start()
{
    // Seed acak hanya dipilih sekali saat awal bila randomSeed aktif
    if (randomSeed)
    {
        seed = NewRandomSeed();
    }

    GenerateDungeon();

    if (cameraScene != null)
    {
        cameraScene.transform.position = new Vector3(mapWidth/2, 45, mapHeight/2);
    }
}

public void GenerateDungeon()
{
    ClearDungeon();

    dungeonValid = false;
    attemptsUsed = 0;
    currentPath = null;
    statusMessage = "";

    int maxAttempts = Mathf.Max(1, maxGenerateAttempts);

    // Generate ulang dengan seed turunan (deterministik) sampai valid
    for (int attempt = 0; attempt < maxAttempts; attempt++)
    {
        attemptsUsed = attempt + 1;

        if (GenerateLayout(GetAttemptSeed(attempt)))
        {
            dungeonValid = true;
            break;
        }
    }

    if (dungeonValid)
    {
        statusMessage = "Valid";

        BuildDungeon();

        SpawnGameplayObjects();
    }
    else
    {
        statusMessage =
            "GAGAL: dungeon valid tidak ditemukan setelah " +
            attemptsUsed + " percobaan";

        Debug.LogError(statusMessage);
    }

    LogGenerationSummary();
}

// Seed turunan per percobaan; langkah bilangan prima agar tidak
// bertabrakan dengan seed + 1 milik tombol R
private int GetAttemptSeed(int attempt)
{
    return unchecked(seed + attempt * 10007);
}

// Bagian logika saja (tanpa Instantiate): true bila layout valid
private bool GenerateLayout(int attemptSeed)
{
    Random.InitState(attemptSeed);

    InitializeGrid();

    CreateBSP();

    CreateRooms();

    CreateCorridors();

    PlaceStartAndGoal();

    enemyPositions.Clear();
    itemPositions.Clear();

    if (!ValidateDungeon())
        return false;

    PlaceObjectPositions();

    return true;
}

private void LogGenerationSummary()
{
    int floorTiles = 0;

    if (grid != null)
    {
        foreach (TileType tile in grid)
        {
            if (tile != TileType.Wall)
                floorTiles++;
        }
    }

    int pathLength =
        currentPath != null ? currentPath.Count - 1 : -1;

    Debug.Log(
        "[DungeonGen] seed=" + seed +
        " attempts=" + attemptsUsed +
        " rooms=" + rooms.Count +
        " floorTiles=" + floorTiles +
        " pathLength=" + pathLength +
        " enemies=" + enemyPositions.Count +
        " items=" + itemPositions.Count +
        " valid=" + dungeonValid
    );
}

private int NewRandomSeed()
{
    return new System.Random(
        System.Environment.TickCount
    ).Next(0, 1000000);
}

// R: seed berikutnya (seed + 1, atau seed baru bila randomSeed aktif)
public void NextSeed()
{
    seedHistory.Add(seed);

    seed = randomSeed ? NewRandomSeed() : seed + 1;

    GenerateDungeon();
}

// T: kembali ke seed sebelumnya
public void PreviousSeed()
{
    if (seedHistory.Count == 0)
        return;

    seed = seedHistory[seedHistory.Count - 1];
    seedHistory.RemoveAt(seedHistory.Count - 1);

    GenerateDungeon();
}

private void Update()
{
    if (Input.GetKeyDown(KeyCode.R))
    {
        NextSeed();
    }

    if (Input.GetKeyDown(KeyCode.T))
    {
        PreviousSeed();
    }
}

private void InitializeGrid()
{
    grid = new TileType[mapWidth, mapHeight];

    for (int x = 0; x < mapWidth; x++)
    {
        for (int y = 0; y < mapHeight; y++)
        {
            grid[x, y] = TileType.Wall;
        }
    }
}

private void CreateBSP()
{
    rootNode = new BSPNode(
        1,
        1,
        mapWidth - 2,
        mapHeight - 2
    );

    leafNodes.Clear();

    SplitNode(rootNode);
}

private void SplitNode(BSPNode node)
{
    if (node.width <= maxLeafSize &&
        node.height <= maxLeafSize)
    {
        leafNodes.Add(node);
        return;
    }

    bool splitHorizontal;

    if (node.width > node.height * 1.25f)
    {
        splitHorizontal = false;
    }
    else if (node.height > node.width * 1.25f)
    {
        splitHorizontal = true;
    }
    else
    {
        splitHorizontal = Random.value > 0.5f;
    }

    if (splitHorizontal)
    {
        SplitHorizontal(node);
    }
    else
    {
        SplitVertical(node);
    }
}

private void SplitHorizontal(BSPNode node)
{
    if (node.height < minLeafSize * 2)
    {
        leafNodes.Add(node);
        return;
    }

    int split = Random.Range(
        minLeafSize,
        node.height - minLeafSize
    );

    node.left = new BSPNode(
        node.x,
        node.y,
        node.width,
        split
    );

    node.right = new BSPNode(
        node.x,
        node.y + split,
        node.width,
        node.height - split
    );

    SplitNode(node.left);
    SplitNode(node.right);
}

private void SplitVertical(BSPNode node)
{
    if (node.width < minLeafSize * 2)
    {
        leafNodes.Add(node);
        return;
    }

    int split = Random.Range(
        minLeafSize,
        node.width - minLeafSize
    );

    node.left = new BSPNode(
        node.x,
        node.y,
        split,
        node.height
    );

    node.right = new BSPNode(
        node.x + split,
        node.y,
        node.width - split,
        node.height
    );

    SplitNode(node.left);
    SplitNode(node.right);
}

private void CreateRooms()
{
    rooms.Clear();

    foreach (BSPNode leaf in leafNodes)
    {
        int maxWidth = leaf.width - roomMargin * 2;
        int maxHeight = leaf.height - roomMargin * 2;

        if (maxWidth < minRoomSize ||
            maxHeight < minRoomSize)
        {
            continue;
        }

        int roomWidth = Random.Range(
            minRoomSize,
            maxWidth + 1
        );

        int roomHeight = Random.Range(
            minRoomSize,
            maxHeight + 1
        );

        int maxXOffset =
            leaf.width - roomWidth - roomMargin;

        int maxYOffset =
            leaf.height - roomHeight - roomMargin;

        int roomX = leaf.x +
                    Random.Range(
                        roomMargin,
                        maxXOffset + 1
                    );

        int roomY = leaf.y +
                    Random.Range(
                        roomMargin,
                        maxYOffset + 1
                    );

        DungeonRoom room =
            new DungeonRoom(
                roomX,
                roomY,
                roomWidth,
                roomHeight
            );

        leaf.room = room;

        rooms.Add(room);

        CarveRoom(room);
    }
}

private void CarveRoom(DungeonRoom room)
{
    for (int x = room.x;
         x < room.x + room.width;
         x++)
    {
        for (int y = room.y;
             y < room.y + room.height;
             y++)
        {
            if (IsInsideGrid(x, y))
            {
                grid[x, y] = TileType.Floor;
            }
        }
    }
}

private bool IsInsideGrid(int x, int y)
{
    return x >= 0 &&
           x < mapWidth &&
           y >= 0 &&
           y < mapHeight;
}

private void CreateLCorridor(
    Vector2Int from,
    Vector2Int to)
{
    bool horizontalFirst =
        Random.value > 0.5f;

    if (horizontalFirst)
    {
        CarveHorizontal(
            from.x,
            to.x,
            from.y
        );

        CarveVertical(
            from.y,
            to.y,
            to.x
        );
    }
    else
    {
        CarveVertical(
            from.y,
            to.y,
            from.x
        );

        CarveHorizontal(
            from.x,
            to.x,
            to.y
        );
    }
}

private void CarveHorizontal(
    int x1,
    int x2,
    int y)
{
    int start = Mathf.Min(x1, x2);
    int end = Mathf.Max(x1, x2);

    // Lebar corridor: baris dipusatkan di sekitar y
    int offset = (corridorWidth - 1) / 2;

    for (int x = start; x <= end; x++)
    {
        for (int w = 0; w < corridorWidth; w++)
        {
            int yy = y - offset + w;

            if (IsInsideGrid(x, yy))
            {
                grid[x, yy] = TileType.Floor;
            }
        }
    }
}

private void CarveVertical(
    int y1,
    int y2,
    int x)
{
    int start = Mathf.Min(y1, y2);
    int end = Mathf.Max(y1, y2);

    // Lebar corridor: kolom dipusatkan di sekitar x
    int offset = (corridorWidth - 1) / 2;

    for (int y = start; y <= end; y++)
    {
        for (int w = 0; w < corridorWidth; w++)
        {
            int xx = x - offset + w;

            if (IsInsideGrid(xx, y))
            {
                grid[xx, y] = TileType.Floor;
            }
        }
    }
}

private DungeonRoom FindFarthestRoom(
    Vector2Int position)
{
    DungeonRoom farthest = rooms[0];
    float farthestDistance = 0f;

    foreach (DungeonRoom room in rooms)
    {
        float distance =
            Vector2Int.Distance(
                position,
                room.Center
            );

        if (distance > farthestDistance)
        {
            farthestDistance = distance;
            farthest = room;
        }
    }

    return farthest;
}

private void PlaceStartAndGoal()
{
    if (rooms.Count == 0)
        return;

    startPosition = rooms[0].Center;

    DungeonRoom farthestRoom =
        FindFarthestRoom(startPosition);

    goalPosition = farthestRoom.Center;

    grid[startPosition.x, startPosition.y]
        = TileType.Start;

    grid[goalPosition.x, goalPosition.y]
        = TileType.Goal;
}
private void CreateLevelRoot()
{
    GameObject root =
        new GameObject("GeneratedLevel");

    levelRoot = root.transform;
}

private void BuildDungeon()
{
    CreateLevelRoot();

    for (int x = 0; x < mapWidth; x++)
    {
        for (int y = 0; y < mapHeight; y++)
        {
            Vector3 worldPosition =
                GridToWorld(x, y);

            switch (grid[x, y])
            {
                case TileType.Floor:
                case TileType.Start:
                case TileType.Goal:

                    Instantiate(
                        floorPrefab,
                        worldPosition,
                        Quaternion.identity,
                        levelRoot
                    );

                    break;
            }

            if (grid[x, y] == TileType.Wall &&
                IsWallVisible(x, y))
            {
                Instantiate(
                    wallPrefab,
                    worldPosition +
                    Vector3.up * 0.5f,
                    Quaternion.identity,
                    levelRoot
                );
            }

            if (grid[x, y] == TileType.Goal)
            {
                Instantiate(
                    goalPrefab,
                    worldPosition +
                    Vector3.up * 0.15f,
                    Quaternion.identity,
                    levelRoot
                );
            }
        }
    }
}

private Vector3 GridToWorld(int x, int y)
{
    return new Vector3(
        x * tileSize,
        0f,
        y * tileSize
    );
}

private bool IsWallVisible(int x, int y)
{
    for (int dx = -1; dx <= 1; dx++)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            int nx = x + dx;
            int ny = y + dy;

            if (!IsInsideGrid(nx, ny))
                continue;

            if (grid[nx, ny] != TileType.Wall)
                return true;
        }
    }

    return false;
}

private void ClearDungeon()
{
    GameObject existing =
        GameObject.Find("GeneratedLevel");

    if (existing != null)
    {
        Destroy(existing);
    }

    GameObject oldPlayer =
        GameObject.FindWithTag("Player");

    if (oldPlayer != null)
    {
        Destroy(oldPlayer);
    }
}

private void CreateCorridors()
{
    if (rooms.Count < 2)
        return;

    for (int i = 0; i < rooms.Count - 1; i++)
    {
        Vector2Int from = rooms[i].Center;
        Vector2Int to = rooms[i + 1].Center;

        CreateLCorridor(from, to);
    }
}

private void SpawnGameplayObjects()
{
    SpawnPlayer();
    SpawnEnemies();
    SpawnItems();
}

private void SpawnPlayer()
{
    if (playerPrefab == null)
        return;

    Vector3 position =
        GridToWorld(
            startPosition.x,
            startPosition.y
        );

    Instantiate(
        playerPrefab,
        position + Vector3.up * 1f,
        Quaternion.identity
    );
}

// Tentukan posisi enemy dan item di data grid (belum instantiate)
private void PlaceObjectPositions()
{
    enemyPositions.Clear();
    itemPositions.Clear();

    // Semua tile yang sudah terisi objek dianggap terblokir
    HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

    for (int i = 0; i < enemyCount; i++)
    {
        Vector2Int pos;

        // Gagal setelah maxPlacementAttempts: lewati enemy ini
        if (TryGetRandomFloorPosition(true, occupied, out pos))
        {
            enemyPositions.Add(pos);
            occupied.Add(pos);
        }
    }

    for (int i = 0; i < itemCount; i++)
    {
        Vector2Int pos;

        if (TryGetRandomFloorPosition(false, occupied, out pos))
        {
            itemPositions.Add(pos);
            occupied.Add(pos);
        }
    }

    // Cek ulang: goal harus tetap terjangkau dengan semua objek terblokir
    while (occupied.Count > 0 &&
           FindPath(startPosition, goalPosition, occupied) == null)
    {
        if (enemyPositions.Count > 0)
        {
            Vector2Int removed = enemyPositions[enemyPositions.Count - 1];
            enemyPositions.RemoveAt(enemyPositions.Count - 1);
            occupied.Remove(removed);
        }
        else if (itemPositions.Count > 0)
        {
            Vector2Int removed = itemPositions[itemPositions.Count - 1];
            itemPositions.RemoveAt(itemPositions.Count - 1);
            occupied.Remove(removed);
        }
        else
        {
            break;
        }
    }
}

private void SpawnEnemies()
{
    if (enemyPrefab == null)
        return;

    for (int i = 0; i < enemyPositions.Count; i++)
    {
        Vector3 worldPosition =
            GridToWorld(
                enemyPositions[i].x,
                enemyPositions[i].y
            );

        GameObject enemy =
            Instantiate(
                enemyPrefab,
                worldPosition +
                Vector3.up * 1f,
                Quaternion.identity,
                levelRoot
            );

        enemy.name = "Enemy_" + i;
    }
}

private void SpawnItems()
{
    if (itemPrefab == null)
        return;

    for (int i = 0; i < itemPositions.Count; i++)
    {
        Vector3 worldPosition =
            GridToWorld(
                itemPositions[i].x,
                itemPositions[i].y
            );

        GameObject item =
            Instantiate(
                itemPrefab,
                worldPosition +
                Vector3.up * 0.5f,
                Quaternion.identity,
                levelRoot
            );

        item.name = "Item_" + i;
    }
}

// Zona aman: radius safeRadius dari start, dan safeRoomCount room pertama
// (room start = rooms[0]) bebas dari enemy
private bool IsInSafeZone(Vector2Int pos)
{
    if (Vector2Int.Distance(pos, startPosition) < safeRadius)
        return true;

    for (int i = 0; i < safeRoomCount && i < rooms.Count; i++)
    {
        DungeonRoom room = rooms[i];

        if (pos.x >= room.x && pos.x < room.x + room.width &&
            pos.y >= room.y && pos.y < room.y + room.height)
        {
            return true;
        }
    }

    return false;
}

// Cari tile Floor biasa (bukan Start/Goal) yang valid.
// Mengembalikan false bila gagal, supaya objek tidak menumpuk di start.
private bool TryGetRandomFloorPosition(
    bool isEnemy,
    HashSet<Vector2Int> occupied,
    out Vector2Int result)
{
    for (int attempt = 0;
         attempt < maxPlacementAttempts;
         attempt++)
    {
        int x = Random.Range(1, mapWidth - 1);
        int y = Random.Range(1, mapHeight - 1);

        if (grid[x, y] != TileType.Floor)
            continue;

        Vector2Int candidate = new Vector2Int(x, y);

        if (occupied.Contains(candidate))
            continue;

        if (isEnemy && IsInSafeZone(candidate))
            continue;

        // Tolak tile yang menutup jalur start ke goal (mis. koridor sempit)
        occupied.Add(candidate);
        bool blocksPath =
            FindPath(startPosition, goalPosition, occupied) == null;
        occupied.Remove(candidate);

        if (blocksPath)
            continue;

        result = candidate;
        return true;
    }

    result = Vector2Int.zero;
    return false;
}

// BFS jalur terpendek (4 arah). Mengembalikan null bila tidak terjangkau.
// Tile pada 'blocked' dianggap dinding.
private List<Vector2Int> FindPath(
    Vector2Int from,
    Vector2Int to,
    HashSet<Vector2Int> blocked)
{
    Vector2Int[,] parent = new Vector2Int[mapWidth, mapHeight];
    bool[,] visited = new bool[mapWidth, mapHeight];

    Queue<Vector2Int> queue = new Queue<Vector2Int>();

    queue.Enqueue(from);
    visited[from.x, from.y] = true;

    Vector2Int[] directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    while (queue.Count > 0)
    {
        Vector2Int current = queue.Dequeue();

        if (current == to)
        {
            List<Vector2Int> path = new List<Vector2Int>();

            for (Vector2Int p = to; p != from; p = parent[p.x, p.y])
            {
                path.Add(p);
            }

            path.Add(from);
            path.Reverse();

            return path;
        }

        foreach (Vector2Int direction in directions)
        {
            Vector2Int next = current + direction;

            if (!IsInsideGrid(next.x, next.y))
                continue;

            if (visited[next.x, next.y])
                continue;

            if (grid[next.x, next.y] == TileType.Wall)
                continue;

            if (blocked != null && blocked.Contains(next))
                continue;

            visited[next.x, next.y] = true;
            parent[next.x, next.y] = current;

            queue.Enqueue(next);
        }
    }

    return null;
}

private void OnDrawGizmos()
{
    if (rooms == null)
        return;

    Gizmos.color = Color.yellow;

    foreach (DungeonRoom room in rooms)
    {
        Vector3 center =
            new Vector3(
                (room.x + room.width / 2f) * tileSize,
                0.2f,
                (room.y + room.height / 2f) * tileSize
            );

        Vector3 size =
            new Vector3(
                room.width * tileSize,
                0.2f,
                room.height * tileSize
            );

        Gizmos.DrawWireCube(
            center,
            size
        );
    }
}

private bool ValidateDungeon()
{
    if (rooms == null || rooms.Count < 2)
    {
        return Fail("jumlah room terlalu sedikit");
    }

    if (!IsInsideGrid(startPosition.x, startPosition.y) ||
        !IsInsideGrid(goalPosition.x, goalPosition.y))
    {
        return Fail("start/goal di luar grid");
    }

    if (startPosition == goalPosition)
    {
        return Fail("start dan goal sama");
    }

    // BFS: goal harus terjangkau dari start
    currentPath = FindPath(startPosition, goalPosition, null);

    if (currentPath == null)
    {
        return Fail("goal tidak dapat dicapai dari start");
    }

    // Flood fill: seluruh room harus terjangkau dari start
    bool[,] reachable = FloodFill(startPosition);

    foreach (DungeonRoom room in rooms)
    {
        Vector2Int center = room.Center;

        if (!reachable[center.x, center.y])
        {
            return Fail("ada room yang tidak terjangkau dari start");
        }
    }

    return true;
}

private bool Fail(string reason)
{
    Debug.LogWarning("Dungeon invalid: " + reason);

    return false;
}

// Flood fill (4 arah) dari origin melalui tile non-Wall
private bool[,] FloodFill(Vector2Int origin)
{
    bool[,] visited = new bool[mapWidth, mapHeight];

    Queue<Vector2Int> queue = new Queue<Vector2Int>();

    queue.Enqueue(origin);
    visited[origin.x, origin.y] = true;

    Vector2Int[] directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    while (queue.Count > 0)
    {
        Vector2Int current = queue.Dequeue();

        foreach (Vector2Int direction in directions)
        {
            Vector2Int next = current + direction;

            if (!IsInsideGrid(next.x, next.y))
                continue;

            if (visited[next.x, next.y])
                continue;

            if (grid[next.x, next.y] == TileType.Wall)
                continue;

            visited[next.x, next.y] = true;

            queue.Enqueue(next);
        }
    }

    return visited;
}

}
