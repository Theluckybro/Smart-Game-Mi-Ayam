using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinder : MonoBehaviour
{
    public GridManager gridManager;

    public Transform startMarker;
    public Transform goalMarker;

    [Header("Debug")]
    public bool showOpenClosed = true;

    public List<GridNode> currentPath =
        new List<GridNode>();

    private void Start()
    {
        FindPath();
    }

    [ContextMenu("Find Path")]
    public void FindPath()
    {
        if (gridManager == null ||
            startMarker == null ||
            goalMarker == null)
        {
            Debug.LogWarning(
                "GridManager, StartMarker, atau GoalMarker belum diisi."
            );
            return;
        }

        gridManager.ResetSearchData();

        GridNode startNode =
            gridManager.NodeFromWorldPosition(
                startMarker.position
            );

        GridNode goalNode =
            gridManager.NodeFromWorldPosition(
                goalMarker.position
            );

        if (!startNode.walkable)
        {
            Debug.LogWarning(
                "Start berada pada obstacle."
            );
            return;
        }

        if (!goalNode.walkable)
        {
            Debug.LogWarning(
                "Goal berada pada obstacle."
            );
            return;
        }

        List<GridNode> openSet =
            new List<GridNode>();

        HashSet<GridNode> closedSet =
            new HashSet<GridNode>();

        startNode.gCost = 0;
        startNode.hCost =
            GetHeuristic(startNode, goalNode);

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            GridNode currentNode =
                GetLowestFCostNode(openSet);

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (showOpenClosed)
            {
                gridManager.SetNodeColor(
                    currentNode,
                    new Color(1f, 0.6f, 0.2f)
                );
            }

            if (currentNode == goalNode)
            {
                currentPath =
                    ReconstructPath(
                        startNode,
                        goalNode
                    );

                VisualizeFinalPath(
                    startNode,
                    goalNode
                );

                Debug.Log(
                    $"Path ditemukan. Node path: {currentPath.Count}"
                );

                return;
            }

            foreach (
                GridNode neighbor
                in gridManager.GetNeighbors(currentNode))
            {
                if (!neighbor.walkable)
                {
                    continue;
                }

                if (closedSet.Contains(neighbor))
                {
                    continue;
                }

                int tentativeGCost =
                    currentNode.gCost + 10;

                if (tentativeGCost <
                    neighbor.gCost)
                {
                    neighbor.parent =
                        currentNode;

                    neighbor.gCost =
                        tentativeGCost;

                    neighbor.hCost =
                        GetHeuristic(
                            neighbor,
                            goalNode
                        );

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }

                    if (showOpenClosed)
                    {
                        gridManager.SetNodeColor(
                            neighbor,
                            Color.yellow
                        );
                    }
                }
            }
        }

        currentPath.Clear();

        Debug.LogWarning(
            "Path tidak ditemukan."
        );
    }

    private GridNode GetLowestFCostNode(
        List<GridNode> openSet)
    {
        GridNode best = openSet[0];

        for (int i = 1;
             i < openSet.Count;
             i++)
        {
            GridNode candidate =
                openSet[i];

            if (candidate.FCost < best.FCost)
            {
                best = candidate;
            }
            else if (
                candidate.FCost == best.FCost &&
                candidate.hCost < best.hCost)
            {
                best = candidate;
            }
        }

        return best;
    }

    private int GetHeuristic(
        GridNode a,
        GridNode b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);

        return (dx + dy) * 10;
    }

    private List<GridNode> ReconstructPath(
        GridNode startNode,
        GridNode goalNode)
    {
        List<GridNode> path =
            new List<GridNode>();

        GridNode current =
            goalNode;

        while (current != null &&
               current != startNode)
        {
            path.Add(current);
            current = current.parent;
        }

        path.Add(startNode);
        path.Reverse();

        return path;
    }

    private void VisualizeFinalPath(
        GridNode startNode,
        GridNode goalNode)
    {
        foreach (GridNode node in currentPath)
        {
            gridManager.SetNodeColor(
                node,
                Color.cyan
            );
        }

        gridManager.SetNodeColor(
            startNode,
            Color.green
        );

        gridManager.SetNodeColor(
            goalNode,
            Color.red
        );
    }
}