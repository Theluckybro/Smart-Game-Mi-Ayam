using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshPathDebugger : MonoBehaviour
{
    private NavMeshAgent agent;

    public Color lineColor = Color.cyan;

    private void Awake()
    {
        agent =
            GetComponent<NavMeshAgent>();
    }

    private void OnDrawGizmos()
    {
        if (agent == null)
        {
            agent =
                GetComponent<NavMeshAgent>();
        }

        if (agent == null ||
            !agent.hasPath)
        {
            return;
        }

        Vector3[] corners =
            agent.path.corners;

        Gizmos.color = lineColor;

        for (int i = 0;
             i < corners.Length - 1;
             i++)
        {
            Gizmos.DrawLine(
                corners[i],
                corners[i + 1]
            );

            Gizmos.DrawSphere(
                corners[i],
                0.12f
            );
        }

        if (corners.Length > 0)
        {
            Gizmos.DrawSphere(
                corners[corners.Length - 1],
                0.12f
            );
        }
    }
}