using UnityEngine;
using UnityEngine.AI;

public class NPCBrain : MonoBehaviour
{
    public enum NPCState
    {
        Patrol,
        Chase,
        Search
    }

    [Header("References")]
    [SerializeField]
    private NPCSensor sensor;

    [SerializeField]
    private NavMeshAgent agent;

    [Header("Patrol Settings")]
    [SerializeField]
    private Transform[] patrolPoints;

    [SerializeField]
    private float waypointTolerance = 0.7f;

    [SerializeField]
    private float patrolSpeed = 2f;

    [Header("Chase Settings")]
    [SerializeField]
    private float chaseSpeed = 4f;

    [Header("Search Settings")]
    [SerializeField]
    private float searchDuration = 4f;

    [SerializeField]
    private float searchTolerance = 0.8f;

    [Header("Debug")]
    [SerializeField]
    private NPCState currentState;

    private NPCState previousState;

    private int patrolIndex = 0;

    // =============================
    // MEMORY
    // =============================

    private Vector3 lastKnownPosition;

    private bool hasLastKnownPosition;

    private float searchTimer;

    private void Start()
    {
        currentState = NPCState.Patrol;
        previousState = currentState;

        GoToCurrentPatrolPoint();
    }

    private void Update()
    {
        UpdateMemory();

        MakeDecision();

        ExecuteCurrentState();
    }

    // ======================================
    // MEMORY
    // ======================================

    private void UpdateMemory()
    {
        if (sensor.CanSeePlayer)
        {
            lastKnownPosition =
                sensor.Player.position;

            hasLastKnownPosition = true;
        }
    }

    // ======================================
    // DECISION
    // ======================================

    private void MakeDecision()
    {
        // PRIORITAS 1
        // PLAYER TERLIHAT
        if (sensor.CanSeePlayer)
        {
            ChangeState(
                NPCState.Chase
            );

            return;
        }

        // PRIORITAS 2
        // PLAYER BARU HILANG
        if (currentState ==
                NPCState.Chase &&
            hasLastKnownPosition)
        {
            searchTimer =
                searchDuration;

            ChangeState(
                NPCState.Search
            );

            return;
        }

        // PRIORITAS 3
        // SEARCH SELESAI
        if (currentState ==
                NPCState.Search &&
            searchTimer <= 0f)
        {
            hasLastKnownPosition =
                false;

            ChangeState(
                NPCState.Patrol
            );
        }
    }

    // ======================================
    // ACTION
    // ======================================

    private void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case NPCState.Patrol:

                Patrol();
                break;

            case NPCState.Chase:

                Chase();
                break;

            case NPCState.Search:

                Search();
                break;
        }
    }

    // ======================================
    // PATROL
    // ======================================

    private void Patrol()
    {
        agent.speed = patrolSpeed;

        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            return;
        }

        if (!agent.pathPending &&
            agent.remainingDistance <=
            waypointTolerance)
        {
            patrolIndex++;

            if (patrolIndex >=
                patrolPoints.Length)
            {
                patrolIndex = 0;
            }

            GoToCurrentPatrolPoint();
        }
    }

    private void GoToCurrentPatrolPoint()
    {
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            return;
        }

        agent.SetDestination(
            patrolPoints[
                patrolIndex
            ].position
        );
    }

    // ======================================
    // CHASE
    // ======================================

    private void Chase()
    {
        agent.speed =
            chaseSpeed;

        if (sensor.Player == null)
            return;

        agent.SetDestination(
            sensor.Player.position
        );
    }

    // ======================================
    // SEARCH
    // ======================================

    private void Search()
    {
        agent.speed =
            patrolSpeed;

        agent.SetDestination(
            lastKnownPosition
        );

        if (!agent.pathPending &&
            agent.remainingDistance <=
            searchTolerance)
        {
            searchTimer -=
                Time.deltaTime;

            agent.ResetPath();
        }
    }

    // ======================================
    // STATE TRANSITION
    // ======================================

    private void ChangeState(
        NPCState newState
    )
    {
        if (currentState ==
            newState)
        {
            return;
        }

        previousState =
            currentState;

        currentState =
            newState;

        Debug.Log(
            gameObject.name +
            ": " +
            previousState +
            " -> " +
            currentState
        );

        if (currentState ==
            NPCState.Patrol)
        {
            GoToCurrentPatrolPoint();
        }
    }

    // ======================================
    // DEBUG GIZMOS
    // ======================================

    private void OnDrawGizmosSelected()
    {
        switch (currentState)
        {
            case NPCState.Patrol:

                Gizmos.color =
                    Color.green;
                break;

            case NPCState.Chase:

                Gizmos.color =
                    Color.red;
                break;

            case NPCState.Search:

                Gizmos.color =
                    Color.blue;
                break;
        }

        Gizmos.DrawWireSphere(
            transform.position,
            0.8f
        );

        if (hasLastKnownPosition)
        {
            Gizmos.color =
                Color.magenta;

            Gizmos.DrawSphere(
                lastKnownPosition,
                0.3f
            );

            Gizmos.DrawLine(
                transform.position,
                lastKnownPosition
            );
        }
    }
}
