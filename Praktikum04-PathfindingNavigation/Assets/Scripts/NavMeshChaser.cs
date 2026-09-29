using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshChaser : MonoBehaviour
{
    public Transform target;

    [Header("Repathing")]
    public float repathInterval = 0.25f;
    public float targetMoveThreshold = 0.5f;

    private NavMeshAgent agent;

    private float nextRepathTime;

    private Vector3 lastTargetPosition;

    private void Awake()
    {
        agent =
            GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (target != null)
        {
            lastTargetPosition =
                target.position;

            UpdateDestination();
        }
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        if (Time.time < nextRepathTime)
        {
            return;
        }

        float targetMoved =
            Vector3.Distance(
                lastTargetPosition,
                target.position
            );

        if (targetMoved >=
            targetMoveThreshold)
        {
            UpdateDestination();
        }

        nextRepathTime =
            Time.time + repathInterval;
    }

    private void UpdateDestination()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning(
                "NPC tidak berada di atas NavMesh."
            );
            return;
        }

        agent.SetDestination(
            target.position
        );

        lastTargetPosition =
            target.position;
    }
}