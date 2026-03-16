using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TankAIDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DriverController driver;
    [SerializeField] private NavMeshAgent agent;

    [Header("Settings")]
    [SerializeField] private float arrivalRadius = 1.5f;
    [SerializeField] private float angleToMove = 25f;
    [SerializeField] private float pivotThreshold = 60f;

    private Vector3 destination;
    private bool hasDestination = false;
    private bool driverDead = false;
    private bool isReversing = false;
    private Vector3 reverseDir;

    private void Update()
    {
        if (driverDead) return;
        if (agent == null || !agent.enabled) return;

        if (isReversing)
        {
            UpdateReverseInput();
            return;
        }

        if (!hasDestination) return;
        if (!agent.isOnNavMesh) return;
        if (!agent.hasPath) return;

        UpdateAIInput();
    }

    private void UpdateAIInput()
    {
        Vector3 nextPoint = agent.steeringTarget;
        Vector3 dir = nextPoint - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 1e-4f)
        {
            driver.SetInput(0f, 0f, 0f);
            return;
        }

        float angle = Vector3.SignedAngle(transform.forward, dir.normalized, Vector3.up);
        float absAngle = Mathf.Abs(angle);

        float throttle;
        float steer;
        float pivot;

        if (absAngle > pivotThreshold)
        {
            throttle = 0f;
            steer = 0f;
            pivot = Mathf.Sign(angle);
        }
        else if (absAngle > angleToMove)
        {
            throttle = 0.5f;
            steer = Mathf.Clamp(angle / 45f, -1f, 1f);
            pivot = 0f;
        }
        else
        {
            throttle = 1f;
            steer = Mathf.Clamp(angle / 45f, -1f, 1f);
            pivot = 0f;
        }

        driver.SetInput(throttle, steer, pivot);
        agent.nextPosition = transform.position;
    }

    private void UpdateReverseInput()
    {
        Vector3 backward = -transform.forward;
        float angle = Vector3.SignedAngle(backward, reverseDir, Vector3.up);
        float steer = Mathf.Clamp(angle / 45f, -1f, 1f);
        driver.SetInput(-1f, steer, 0f);
    }

    public void SetReverseDestination(Vector3 awayFrom)
    {
        isReversing = true;
        hasDestination = false;

        Vector3 dir = transform.position - awayFrom;
        dir.y = 0f;
        reverseDir = dir.sqrMagnitude > 1e-4f ? dir.normalized : -transform.forward;
    }

    public void SetDestination(Vector3 dest)
    {
        isReversing = false;
        destination = dest;
        hasDestination = true;

        if (agent == null || !agent.enabled)
        {
            hasDestination = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var selfHit, 8f, NavMesh.AllAreas))
                agent.Warp(selfHit.position);
            else
            {
                hasDestination = false;
                return;
            }
        }

        if (NavMesh.SamplePosition(dest, out var destHit, 8f, NavMesh.AllAreas))
            agent.SetDestination(destHit.position);
        else
            agent.SetDestination(dest);
    }

    public void Stop()
    {
        isReversing = false;
        hasDestination = false;
        driver.SetInput(0f, 0f, 0f);
    }

    public void SetDriverDead()
    {
        driverDead = true;
        if (agent != null)
            agent.enabled = false;
        Stop();
    }

    public bool IsArrived()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;

        return !agent.pathPending && agent.remainingDistance < arrivalRadius;
    }
}
