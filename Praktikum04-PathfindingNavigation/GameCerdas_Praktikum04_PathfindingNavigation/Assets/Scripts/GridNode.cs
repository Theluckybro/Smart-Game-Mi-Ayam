using UnityEngine;

public class GridNode
{
    public int x;
    public int y;

    public Vector3 worldPosition;

    public bool walkable;

    public int gCost;
    public int hCost;

    public GridNode parent;

    public GameObject visual;

    public int FCost
    {
        get
        {
            return gCost + hCost;
        }
    }

    public GridNode(
        int x,
        int y,
        Vector3 worldPosition,
        bool walkable)
    {
        this.x = x;
        this.y = y;
        this.worldPosition = worldPosition;
        this.walkable = walkable;

        gCost = int.MaxValue;
        hCost = 0;
        parent = null;
    }
}