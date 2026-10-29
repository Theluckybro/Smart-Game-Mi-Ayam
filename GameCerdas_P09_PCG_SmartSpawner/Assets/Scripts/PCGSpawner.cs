
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PCGSpawner : MonoBehaviour
{
    public enum EnemyColor
    {
        None,
        Red,
        Blue,
        Yellow
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Legendary
    }

    [System.Serializable]
    public class SpawnEntry
    {
        public string name;
        public GameObject prefab;

        [Min(0)]
        public int weight = 1;

        [Header("Spawn Settings")]
        public float yOffset = 0f;
        public bool randomYRotation = true;

        [Header("Enemy Statistics")]
        public EnemyColor enemyColor = EnemyColor.None;

        [Header("Item Rarity")]
        public ItemRarity itemRarity = ItemRarity.Common;

        [Header("Difficulty")]
        [Min(0)]
        public int difficultyCost = 1;
    }

    [Header("Seed")]
    [SerializeField]
    private bool useRandomSeed = false;

    [SerializeField]
    private int seed = 12345;

    [SerializeField]
    private int seedHistoryLimit = 5;

    [SerializeField]
    private List<int> seedHistory = new List<int>();

    [Header("References")]
    [SerializeField]
    private Transform player;

    [SerializeField]
    private Transform generatedRoot;

    [Header("Spawn Zone")]
    [SerializeField]
    private string zoneName = "Default Zone";

    [SerializeField]
    private Vector2 spawnAreaSize = new Vector2(24f, 24f);

    [SerializeField]
    private float spawnY = 0.5f;

    [Header("Spawn Counts")]
    [SerializeField]
    private int enemyCount = 12;

    [SerializeField]
    private int itemCount = 8;

    [Header("Difficulty Budget")]
    [SerializeField]
    private bool useDifficultyBudget = true;

    [Min(0)]
    [SerializeField]
    private int difficultyBudget = 20;

    [SerializeField]
    private int remainingDifficultyBudget;

    [Header("Validation")]
    [SerializeField]
    private float minDistanceFromPlayer = 5f;

    [SerializeField]
    private float checkRadius = 0.75f;

    [SerializeField]
    private float minimumSpacing = 1.5f;

    [SerializeField]
    private LayerMask obstacleMask;

    [SerializeField]
    private int maxAttemptsPerObject = 50;

    [Header("NavMesh Validation")]
    [SerializeField]
    private bool useNavMeshValidation = true;

    [Min(0.1f)]
    [SerializeField]
    private float navMeshSampleRadius = 2f;

    [SerializeField]
    private int navMeshAreaMask = NavMesh.AllAreas;

    [Header("Enemy Weighted Table")]
    [SerializeField]
    private SpawnEntry[] enemyEntries;

    [Header("Item Weighted Table")]
    [SerializeField]
    private SpawnEntry[] itemEntries;

    [Header("Runtime Debug")]
    [SerializeField]
    private int currentSeed;

    [SerializeField]
    private int successfulEnemySpawns;

    [SerializeField]
    private int successfulItemSpawns;

    [SerializeField]
    private int rejectedPositions;

    [SerializeField]
    private int totalEnemyDifficulty;

    [Header("Enemy Color Statistics")]
    [SerializeField]
    private int redCount;

    [SerializeField]
    private int blueCount;

    [SerializeField]
    private int yellowCount;

    [Header("Item Rarity Statistics")]
    [SerializeField]
    private int commonCount;

    [SerializeField]
    private int uncommonCount;

    [SerializeField]
    private int rareCount;

    [SerializeField]
    private int legendaryCount;

    private readonly List<Vector3> usedPositions =
        new List<Vector3>();

    private readonly List<GameObject> generatedObjects =
        new List<GameObject>();

    private void Start()
    {
        Generate();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Generate();
        }
    }

    public void Generate()
    {
        GenerateWithEnemyCount(enemyCount);
    }

    private void GenerateWithEnemyCount(int requestedEnemyCount)
    {
        ClearGeneratedObjects();
        usedPositions.Clear();

        successfulEnemySpawns = 0;
        successfulItemSpawns = 0;
        rejectedPositions = 0;
        totalEnemyDifficulty = 0;

        redCount = 0;
        blueCount = 0;
        yellowCount = 0;

        commonCount = 0;
        uncommonCount = 0;
        rareCount = 0;
        legendaryCount = 0;

        remainingDifficultyBudget = difficultyBudget;

        if (useRandomSeed)
        {
            seed = System.Environment.TickCount;
        }

        currentSeed = seed;
        Random.InitState(currentSeed);
        AddSeedToHistory(currentSeed);

        SpawnGroup(enemyEntries, requestedEnemyCount, true);
        SpawnGroup(itemEntries, itemCount, false);

        Debug.Log(
            $"PCG Generation Complete | " +
            $"Zone: {zoneName} | " +
            $"Seed: {currentSeed} | " +
            $"Enemies: {successfulEnemySpawns}/{requestedEnemyCount} | " +
            $"Items: {successfulItemSpawns}/{itemCount} | " +
            $"Red: {redCount} | Blue: {blueCount} | " +
            $"Yellow: {yellowCount} | " +
            $"Difficulty: {totalEnemyDifficulty}/" +
            $"{(useDifficultyBudget ? difficultyBudget : -1)} | " +
            $"Rejected Positions: {rejectedPositions}"
        );

        Debug.Log(
            $"Item Rarity | Common: {commonCount} | " +
            $"Uncommon: {uncommonCount} | " +
            $"Rare: {rareCount} | Legendary: {legendaryCount}"
        );
    }

    private void AddSeedToHistory(int newSeed)
    {
        seedHistory.Remove(newSeed);
        seedHistory.Insert(0, newSeed);

        while (seedHistory.Count > Mathf.Max(1, seedHistoryLimit))
        {
            seedHistory.RemoveAt(seedHistory.Count - 1);
        }
    }

    private void SpawnGroup(
        SpawnEntry[] entries,
        int count,
        bool enemyGroup)
    {
        if (entries == null || entries.Length == 0)
        {
            Debug.LogWarning(
                $"[{zoneName}] Spawn table kosong."
            );
            return;
        }

        for (int i = 0; i < count; i++)
        {
            SpawnEntry selectedEntry = null;

            if (enemyGroup)
            {
                selectedEntry = SelectWeightedEntry(
                    entries,
                    useDifficultyBudget
                        ? remainingDifficultyBudget
                        : int.MaxValue
                );

                if (selectedEntry == null)
                {
                    Debug.LogWarning(
                        $"[{zoneName}] Enemy generation stopped. " +
                        "No eligible enemy fits the remaining budget."
                    );
                    break;
                }
            }

            bool spawned = false;

            for (
                int attempt = 0;
                attempt < maxAttemptsPerObject;
                attempt++)
            {
                Vector3 candidate = GenerateRandomPosition();

                if (useNavMeshValidation)
                {
                    NavMeshHit hit;

                    if (!NavMesh.SamplePosition(
                        candidate,
                        out hit,
                        navMeshSampleRadius,
                        navMeshAreaMask))
                    {
                        rejectedPositions++;
                        continue;
                    }

                    candidate = hit.position;
                }

                if (!IsPositionValid(candidate))
                {
                    rejectedPositions++;
                    continue;
                }

                if (!enemyGroup)
                {
                    selectedEntry = SelectWeightedEntry(
                        entries,
                        int.MaxValue
                    );

                    if (selectedEntry == null)
                    {
                        Debug.LogWarning(
                            $"[{zoneName}] No valid prefab " +
                            "in the item weighted table."
                        );
                        return;
                    }
                }

                GameObject selectedPrefab = selectedEntry.prefab;

                Quaternion rotation = Quaternion.identity;

                if (selectedEntry.randomYRotation)
                {
                    rotation = Quaternion.Euler(
                        0f,
                        Random.Range(0f, 360f),
                        0f
                    );
                }

                Vector3 spawnPosition = candidate;
                spawnPosition.y += selectedEntry.yOffset;

                GameObject spawnedObject = Instantiate(
                    selectedPrefab,
                    spawnPosition,
                    rotation,
                    generatedRoot
                );

                spawnedObject.name =
                    $"{selectedPrefab.name}_Generated_{i}";

                generatedObjects.Add(spawnedObject);
                usedPositions.Add(candidate);

                if (enemyGroup)
                {
                    successfulEnemySpawns++;

                    totalEnemyDifficulty +=
                        selectedEntry.difficultyCost;

                    if (useDifficultyBudget)
                    {
                        remainingDifficultyBudget -=
                            selectedEntry.difficultyCost;
                    }

                    CountEnemyColor(selectedEntry.enemyColor);
                }
                else
                {
                    successfulItemSpawns++;

                    CountItemRarity(selectedEntry.itemRarity);
                    ApplyRarityVisual(
                        spawnedObject,
                        selectedEntry.itemRarity
                    );
                }

                spawned = true;
                break;
            }

            if (!spawned)
            {
                Debug.LogWarning(
                    $"[{zoneName}] Failed to spawn object #{i}. " +
                    $"No valid position after " +
                    $"{maxAttemptsPerObject} attempts."
                );
            }
        }
    }

    private void CountEnemyColor(EnemyColor color)
    {
        switch (color)
        {
            case EnemyColor.Red:
                redCount++;
                break;

            case EnemyColor.Blue:
                blueCount++;
                break;

            case EnemyColor.Yellow:
                yellowCount++;
                break;
        }
    }

    private void CountItemRarity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:
                commonCount++;
                break;

            case ItemRarity.Uncommon:
                uncommonCount++;
                break;

            case ItemRarity.Rare:
                rareCount++;
                break;

            case ItemRarity.Legendary:
                legendaryCount++;
                break;
        }
    }

    private void ApplyRarityVisual(
        GameObject spawnedObject,
        ItemRarity rarity)
    {
        Color rarityColor;

        switch (rarity)
        {
            case ItemRarity.Common:
                rarityColor = Color.white;
                break;

            case ItemRarity.Uncommon:
                rarityColor = Color.green;
                break;

            case ItemRarity.Rare:
                rarityColor = Color.blue;
                break;

            case ItemRarity.Legendary:
                rarityColor = new Color(1f, 0.65f, 0f);
                break;

            default:
                rarityColor = Color.white;
                break;
        }

        Renderer[] renderers =
            spawnedObject.GetComponentsInChildren<Renderer>();

        MaterialPropertyBlock propertyBlock =
            new MaterialPropertyBlock();

        foreach (Renderer itemRenderer in renderers)
        {
            itemRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(
                "_BaseColor",
                rarityColor
            );

            propertyBlock.SetColor(
                "_Color",
                rarityColor
            );

            itemRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private Vector3 GenerateRandomPosition()
    {
        float halfWidth = spawnAreaSize.x * 0.5f;
        float halfDepth = spawnAreaSize.y * 0.5f;

        float randomX = Random.Range(-halfWidth, halfWidth);
        float randomZ = Random.Range(-halfDepth, halfDepth);

        return transform.position +
            new Vector3(randomX, spawnY, randomZ);
    }

    private bool IsPositionValid(Vector3 candidate)
    {
        if (player != null)
        {
            Vector3 playerFlat = new Vector3(
                player.position.x,
                candidate.y,
                player.position.z
            );

            float distanceToPlayer = Vector3.Distance(
                candidate,
                playerFlat
            );

            if (distanceToPlayer < minDistanceFromPlayer)
            {
                return false;
            }
        }

        bool blocked = Physics.CheckSphere(
            candidate,
            checkRadius,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        if (blocked)
        {
            return false;
        }

        for (int i = 0; i < usedPositions.Count; i++)
        {
            float distance = Vector3.Distance(
                candidate,
                usedPositions[i]
            );

            if (distance < minimumSpacing)
            {
                return false;
            }
        }

        return true;
    }

    private SpawnEntry SelectWeightedEntry(
        SpawnEntry[] entries,
        int maximumCost)
    {
        int totalWeight = 0;

        foreach (SpawnEntry entry in entries)
        {
            if (
                entry.prefab != null &&
                entry.weight > 0 &&
                entry.difficultyCost >= 0 &&
                entry.difficultyCost <= maximumCost
            )
            {
                totalWeight += entry.weight;
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        int randomValue = Random.Range(0, totalWeight);
        int cumulativeWeight = 0;

        foreach (SpawnEntry entry in entries)
        {
            if (
                entry.prefab == null ||
                entry.weight <= 0 ||
                entry.difficultyCost < 0 ||
                entry.difficultyCost > maximumCost
            )
            {
                continue;
            }

            cumulativeWeight += entry.weight;

            if (randomValue < cumulativeWeight)
            {
                return entry;
            }
        }

        return null;
    }

    private void ClearGeneratedObjects()
    {
        for (int i = generatedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = generatedObjects[i];

            if (obj != null)
            {
                obj.SetActive(false);
                Destroy(obj);
            }
        }

        generatedObjects.Clear();
    }

    private void OnGUI()
    {
        GUI.Box(
            new Rect(10, 10, 310, 490),
            $"PCG Debug - {zoneName}"
        );

        GUI.Label(
            new Rect(25, 40, 280, 20),
            $"Seed: {currentSeed}"
        );

        GUI.Label(
            new Rect(25, 62, 280, 20),
            $"Enemy: {successfulEnemySpawns}"
        );

        GUI.Label(
            new Rect(25, 84, 280, 20),
            $"Item: {successfulItemSpawns}"
        );

        GUI.Label(
            new Rect(25, 106, 280, 20),
            $"Difficulty Used: {totalEnemyDifficulty}"
        );

        GUI.Label(
            new Rect(25, 128, 280, 20),
            $"Budget Remaining: " +
            $"{(useDifficultyBudget ? remainingDifficultyBudget : -1)}"
        );

        GUI.Label(
            new Rect(25, 158, 280, 20),
            "--- Enemy Color Statistics ---"
        );

        GUI.Label(
            new Rect(25, 180, 280, 20),
            $"Red: {redCount} | Blue: {blueCount}"
        );

        GUI.Label(
            new Rect(25, 202, 280, 20),
            $"Yellow: {yellowCount}"
        );

        GUI.Label(
            new Rect(25, 232, 280, 20),
            "--- Item Rarity Statistics ---"
        );

        GUI.Label(
            new Rect(25, 254, 280, 20),
            $"Common: {commonCount}"
        );

        GUI.Label(
            new Rect(25, 276, 280, 20),
            $"Uncommon: {uncommonCount}"
        );

        GUI.Label(
            new Rect(25, 298, 280, 20),
            $"Rare: {rareCount} | Legendary: {legendaryCount}"
        );

        GUI.Label(
            new Rect(25, 328, 280, 20),
            "--- Enemy Count Experiments ---"
        );

        if (GUI.Button(new Rect(25, 350, 80, 25), "100"))
        {
            GenerateWithEnemyCount(100);
        }

        if (GUI.Button(new Rect(115, 350, 80, 25), "500"))
        {
            GenerateWithEnemyCount(500);
        }

        if (GUI.Button(new Rect(205, 350, 80, 25), "1000"))
        {
            GenerateWithEnemyCount(1000);
        }

        GUI.Label(
            new Rect(25, 382, 280, 20),
            "--- Recent Seeds ---"
        );

        int displayedSeeds = Mathf.Min(seedHistory.Count, 5);

        for (int i = 0; i < displayedSeeds; i++)
        {
            int historySeed = seedHistory[i];

            if (GUI.Button(
                new Rect(25, 404 + i * 25, 260, 22),
                $"Replay Seed: {historySeed}"))
            {
                seed = historySeed;
                useRandomSeed = false;
                Generate();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Vector3 center = transform.position +
            Vector3.up * spawnY;

        Vector3 size = new Vector3(
            spawnAreaSize.x,
            0.1f,
            spawnAreaSize.y
        );

        Gizmos.DrawWireCube(center, size);

        if (player != null)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(
                player.position,
                minDistanceFromPlayer
            );
        }
    }
}
