using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TankAIController : MonoBehaviour
{
    public enum State { Patrol, Combat, Retreat }

    [Header("Refs")]
    [SerializeField] private TankAIDriver driver;
    [SerializeField] private TankAIGunner gunner;
    [SerializeField] private LoaderController loader;
    [SerializeField] private Transform turret;
    [SerializeField] private AIProfile profile;
    [SerializeField] private Transform player;

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;    // 인스펙터에서 웨이포인트 할당
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private bool loopWaypoints = true; // 끝까지 가면 처음으로

    [Header("Detection")]
    [SerializeField] private LayerMask occluderMask;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.5f, 0f); // 눈 위치

    private enum CombatPhase { Sniping, Advancing }
    private CombatPhase combatPhase = CombatPhase.Sniping;
    private float phaseTimer = 0f;
    private float fireTimer = 0f;
    private float reactionTimer = 0f;
    private bool isReacting = false;

    [Header("Combat Phase")]
    [SerializeField] private float snipingDuration = 5f;
    [SerializeField] private float advanceDuration = 3f;

    private State currentState = State.Patrol;
    private int waypointIndex = 0;
    private float patrolWaitTimer = 0f;
    private bool isWaiting = false;
    private bool isActive = false;
    private void Awake()
    {
        if (player == null)
            Debug.LogWarning("[TankAI] Player 오브젝트를 찾을 수 없습니다!");

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("[TankAI] 웨이포인트 없음, 경계 상태로 대기");
            isActive = true; // ← 이거 추가
            return;
        }
        isActive = true;
        GoToWaypoint(0);
    }

    private void Update()
    {
        if (!isActive) return;

        switch (currentState)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Combat: UpdateCombat(); break;
            case State.Retreat: UpdateRetreat(); break;
        }
    }

    // ===== PATROL =====
    private void UpdatePatrol()
    {
        if (CanDetectPlayer()) // 순찰 중엔 시야각 체크
        {
            ChangeState(State.Combat);
            return;
        }

        if (isWaiting)
        {
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0f)
            {
                isWaiting = false;
                GoToNextWaypoint();
            }
            return;
        }

        if (driver.IsArrived())
        {
            isWaiting = true;
            patrolWaitTimer = patrolWaitTime;
            driver.Stop();
            Debug.Log($"[TankAI] 웨이포인트 {waypointIndex} 도착, 대기 중...");
        }
    }

    private void GoToWaypoint(int index)
    {
        if (waypoints == null || waypoints.Length == 0) return;

        waypointIndex = index % waypoints.Length;
        driver.SetDestination(waypoints[waypointIndex].position);
        Debug.Log($"[TankAI] 웨이포인트 {waypointIndex} 이동");
    }

    private void GoToNextWaypoint()
    {
        if (!loopWaypoints && waypointIndex >= waypoints.Length - 1)
        {
            // 루프 안 하면 마지막 웨이포인트에서 정지
            driver.Stop();
            return;
        }

        GoToWaypoint((waypointIndex + 1) % waypoints.Length);
    }

    // ===== DETECTION =====
    private bool CanSeePlayer()
    {
        if (player == null)
        {
            Debug.Log("[TankAI] player가 NULL!"); 
            return false;
        }

        Vector3 eyePos = transform.position + eyeOffset;
        Vector3 toPlayer = player.position - eyePos;
        float dist = toPlayer.magnitude;

        //거리 체크
        if (dist > profile.detectionRange) return false;
       // Debug.Log($"[TankAI] dist={dist:0.0} detectionRange={profile.detectionRange} angle={Vector3.Angle(transform.forward, toPlayer):0.0} fov={profile.fieldOfView}"); // 추가

        //레이캐스트로 장애물 체크
        if (Physics.Raycast(eyePos, toPlayer.normalized, out var hit, dist, occluderMask))
        {
            // 장애물에 막힘
            Debug.DrawLine(eyePos, hit.point, Color.red);
            Debug.Log($"[TankAI] 장애물에 막힘: {hit.collider.gameObject.name}");
            return false;
        }

        // 플레이어 직접 보임
        Debug.DrawLine(eyePos, player.position, Color.green);
        return true;
    }

    // ===== COMBAT =====
    private void UpdateCombat()
    {
       
        if (!CanSeePlayer())
        {
            isReacting = false;
            ChangeState(State.Patrol);
            return;
        }

        // 반응 딜레이
        if (!isReacting)
        {
            isReacting = true;
            reactionTimer = profile.reactionTime;
            driver.Stop();
            Debug.Log("[TankAI] 반응 딜레이 시작");
            return;
        }
        if (reactionTimer > 0f)
        {
            reactionTimer -= Time.deltaTime;
            return;
        }

        phaseTimer -= Time.deltaTime;

        switch (combatPhase)
        {
            case CombatPhase.Sniping:
                driver.Stop();
                if (phaseTimer <= 0f)
                {
                    combatPhase = CombatPhase.Advancing;
                    phaseTimer = advanceDuration;
                }
                break;

            case CombatPhase.Advancing:
                float dist = Vector3.Distance(transform.position, player.position);
                if (dist > profile.preferredCombatRange)
                    driver.SetDestination(player.position);
                else
                    driver.Stop();

                if (phaseTimer <= 0f)
                {
                    combatPhase = CombatPhase.Sniping;
                    phaseTimer = snipingDuration;
                    driver.Stop();
                }
                break;
        }

        // 포탑 조준 + 사격은 항상
        AimTurretAtPlayer();

        ITankLoader loaderFunc = loader as ITankLoader;
        if (loaderFunc.GetIsLoaded() && IsTurretAimed())
        {
            Shoot();
        }
    }

    private void UpdateRetreat()
    {
        // 다음 단계에서 구현
    }

    private void ChangeState(State next)
    {
        Debug.Log($"[TankAI] {currentState} → {next}");
        currentState = next;

        if (next == State.Combat)
        {
            (loader as ITankLoader).Load(AmmoType.AP);
            combatPhase = CombatPhase.Sniping;
            phaseTimer = snipingDuration;
            isReacting = false;
            fireTimer = profile.reactionTime; // 첫 발은 반응시간 후
        }
    }

    // 씬 뷰에서 감지 범위 시각화
    private void OnDrawGizmosSelected()
    {
        if (profile == null) return;

        // 감지 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, profile.detectionRange);

        // 시야각
        Vector3 leftDir = Quaternion.Euler(0, -profile.fieldOfView * 0.5f, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, profile.fieldOfView * 0.5f, 0) * transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, leftDir * profile.detectionRange);
        Gizmos.DrawRay(transform.position, rightDir * profile.detectionRange);

        // 웨이포인트 경로
        if (waypoints == null || waypoints.Length < 2) return;
        Gizmos.color = Color.white;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.4f);
            Gizmos.DrawLine(waypoints[i].position,
                waypoints[(i + 1) % waypoints.Length].position);
        }
    }
    private bool CanDetectPlayer()
    {
        if (!CanSeePlayer()) return false;

        Vector3 toPlayer = player.position - transform.position;
        float angle = Vector3.Angle(transform.forward, toPlayer);
        return angle < profile.fieldOfView * 0.5f; // 시야각은 최초 감지에만
    }
    private void AimTurretAtPlayer()
    {
        Vector3 targetPos = player.position;

        if (profile.useLeadTarget)
        {
            var playerRb = player.GetComponent<Rigidbody>();
            float dist = Vector3.Distance(turret.position, player.position);
            float tof = dist / 800f;
            if (playerRb != null) targetPos += playerRb.velocity * tof;
        }

        ShellData shellData = (loader as ITankLoader).GetLoadedShell();
        gunner.SetAimTarget(targetPos, shellData);
    }
    public void Die()
    {
        enabled = false;
        driver.SetDriverDead();
        gunner.SetGunnerDead();
               
        Debug.LogWarning($"[TankAI] {gameObject.name} 사망 -> AI 중지");
    }

    private bool IsTurretAimed() => gunner.IsAimed(5f);

    private void Shoot() => gunner.Fire();
}
