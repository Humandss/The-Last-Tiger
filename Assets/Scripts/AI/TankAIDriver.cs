using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TankAIDriver : MonoBehaviour
{


    [Header("Refs")]
    [SerializeField] private DriverController driver; // 플레이어랑 동일한 컴포넌트 재사용
    [SerializeField] private NavMeshAgent agent;

    [Header("Settings")]
    [SerializeField] private float arrivalRadius = 1.5f;    // 도착 판정
    [SerializeField] private float angleToMove = 25f;       // 이 각도 이내면 전진
    [SerializeField] private float pivotThreshold = 60f;    // 이 각도 넘으면 제자리 회전

    private Vector3 destination;
    private bool hasDestination = false;
    private bool driverDead = false;

    private void Update()
    {
        if(driverDead) return;

        if (!hasDestination) return;
        if (!agent.hasPath) return;

        UpdateAIInput();
    }

    private void UpdateAIInput()
    {
        // NavMesh가 계산한 다음 스티어링 포인트
        Vector3 nextPoint = agent.steeringTarget;
        Vector3 dir = nextPoint - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 1e-4f)
        {
            // 도착
            driver.SetInput(0f, 0f, 0f);
            return;
        }

        float angle = Vector3.SignedAngle(transform.forward, dir.normalized, Vector3.up);
        float absAngle = Mathf.Abs(angle);

        float throttle, steer, pivot;

        if (absAngle > pivotThreshold)
        {
            // 많이 틀어졌으면 제자리 회전 우선
            throttle = 0f;
            steer = 0f;
            pivot = Mathf.Sign(angle);
        }
        else if (absAngle > angleToMove)
        {
            // 조금 틀어졌으면 전진하면서 조향
            throttle = 0.5f;
            steer = Mathf.Clamp(angle / 45f, -1f, 1f);
            pivot = 0f;
        }
        else
        {
            // 방향 맞으면 전진
            throttle = 1f;
            steer = Mathf.Clamp(angle / 45f, -1f, 1f);
            pivot = 0f;
        }

        driver.SetInput(throttle, steer, pivot);

        // NavMeshAgent 위치 동기화 (경로 계산용)
        agent.nextPosition = transform.position;
    }

    public void SetDestination(Vector3 dest)
    {
        destination = dest;
        hasDestination = true;
        agent.SetDestination(dest);
    }

    public void Stop()
    {
        hasDestination = false;
        driver.SetInput(0f, 0f, 0f);
    }
    
    public void SetDriverDead()
    {
        driverDead = true;
        agent.enabled = false;
        Stop();
    }
    public bool IsArrived()
    {
        return !agent.pathPending && agent.remainingDistance < arrivalRadius;
    }
}
