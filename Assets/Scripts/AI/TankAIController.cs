using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
    [SerializeField] private TankCrewManager crewManager;

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;    // 인스펙터에서 웨이포인트 할당
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private bool loopWaypoints = true; // 끝까지 가면 처음으로

    [Header("Detection")]
    [SerializeField] private LayerMask occluderMask;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.5f, 0f); // 눈 위치

    [Header("Detection Penalty (커맨더 사망 시)")]
    [SerializeField, Range(0f, 1f)] private float commanderDeadRangeMul = 0.5f;  // 감지 거리 배율
    [SerializeField, Range(0f, 1f)] private float commanderDeadFovMul = 0.6f;  // 시야각 배율
    private bool isCommanderDead = false;

    [Header("Retreat")]
    [SerializeField] private float reverseTime = 3f;            // 후진 지속 시간
    [SerializeField] private float retreatDistance = 40f;       // 도주 목적지 반경
    private enum RetreatPhase { Reversing, Fleeing }
    private RetreatPhase retreatPhase;
    private float reverseTimer = 0f;
    private Vector3 retreatDestination;
    private bool hasRetreatDestination = false;

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

        if (crewManager != null && profile != null)
            crewManager.SetSwapDelay(profile.crewSwapDelay);

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

        if (currentState != State.Retreat && ShouldRetreat())
        {
            ChangeState(State.Retreat);
            return;
        }

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
        // Debug.Log($"[TankAI] dist={dist:0.0} detectionRange={profile.detectionRange} angle={Vector3.Angle(transform.forward, toPlayer):0.0} fov={profile.fieldOfView}"); // 추가
        float effectiveRange = profile.detectionRange *
          (isCommanderDead ? commanderDeadRangeMul : 1f);

        if (dist > effectiveRange) return false;

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
        if(!gunner.IsGunnerDead()) AimTurretAtPlayer();

        switch (retreatPhase)
        {
            case RetreatPhase.Reversing:
                UpdateReversing();
                break;
            case RetreatPhase.Fleeing:
                UpdateFleeing();
                break;
        }
    }

    // ===== RETREAT 조건 =====

    private bool ShouldRetreat()
    {
        if (crewManager == null) return false;

        // 사격 불가 (거너/로더 크루 or 포신/브리치 파괴)
        bool cannotFire = !gunner.CanFire;

        // 운용 불가 (커맨더 있는 탱크: 커맨더+거너+드라이버 / 없는 탱크: 거너+드라이버)
        bool cannotOperate = !crewManager.CanOperate();
        Debug.Log($"[ShouldRetreat] CanFire={gunner.CanFire} cannotOperate={cannotOperate}");
        return cannotFire || cannotOperate;
    }
    private void UpdateReversing()
    {
        reverseTimer -= Time.deltaTime;

        // 플레이어 반대 방향으로 후진
        if (player != null)
        {
            Vector3 awayDir = (transform.position - player.position).normalized;
            Vector3 reverseTarget = transform.position + awayDir * 5f;
            driver.SetReverseDestination(reverseTarget);
        }

        // 후진 시간 끝나면 도주 페이즈로
        if (reverseTimer <= 0f)
        {
            retreatPhase = RetreatPhase.Fleeing;
            TrySetFleeDestination();
            Debug.Log("[TankAI] 후진 완료 → 도주 시작");
        }
    }

    private void UpdateFleeing()
    {
        // 목적지 없으면 재탐색
        if (!hasRetreatDestination)
        {
            if (!TrySetFleeDestination())
            {
                driver.Stop();
                return;
            }
        }

        // 도착하면 정지
        if (driver.IsArrived())
        {
            driver.Stop();
            Debug.Log("[TankAI] 도주 완료, 정지");
            return;
        }

        driver.SetDestination(retreatDestination);
    }

    private bool TrySetFleeDestination()
    {
        if (player == null) return false;

        Vector3 awayDir = (transform.position - player.position).normalized;
        Vector3 candidate = transform.position + awayDir * retreatDistance;

        if (NavMesh.SamplePosition(candidate, out var hit, retreatDistance * 0.5f, NavMesh.AllAreas))
        {
            retreatDestination = hit.position;
            hasRetreatDestination = true;
            Debug.Log($"[TankAI] 도주 목적지: {retreatDestination}");
            return true;
        }

        // 실패 시 45도씩 돌려가며 재시도
        for (int i = 1; i < 8; i++)
        {
            Vector3 rotDir = Quaternion.Euler(0, i * 45f, 0) * awayDir;
            Vector3 rotCandidate = transform.position + rotDir * retreatDistance;

            if (NavMesh.SamplePosition(rotCandidate, out var rotHit, retreatDistance * 0.5f, NavMesh.AllAreas))
            {
                retreatDestination = rotHit.position;
                hasRetreatDestination = true;
                Debug.Log($"[TankAI] 도주 목적지(재시도): {retreatDestination}");
                return true;
            }
        }

        Debug.LogWarning("[TankAI] 도주 목적지를 찾을 수 없음!");
        return false;
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
        else if (next == State.Retreat)
        {
            retreatPhase = RetreatPhase.Reversing;
            reverseTimer = reverseTime;
            hasRetreatDestination = false;
            driver.Stop();
            Debug.Log("[TankAI] 후퇴 시작 → 후진 페이즈");
        }
    }

    // 씬 뷰에서 감지 범위 시각화
    private void OnDrawGizmosSelected()
    {
        if (profile == null) return;

        float effectiveRange = profile.detectionRange * (isCommanderDead ? commanderDeadRangeMul : 1f);
        float effectiveFov = profile.fieldOfView * (isCommanderDead ? commanderDeadFovMul : 1f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, effectiveRange);

        Vector3 leftDir = Quaternion.Euler(0, -effectiveFov * 0.5f, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, effectiveFov * 0.5f, 0) * transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, leftDir * effectiveRange);
        Gizmos.DrawRay(transform.position, rightDir * effectiveRange);

        // 후퇴 목적지 표시
        if (hasRetreatDestination)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(retreatDestination, 1f);
            Gizmos.DrawLine(transform.position, retreatDestination);
        }

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

        float effectiveFov = profile.fieldOfView * (isCommanderDead ? commanderDeadFovMul : 1f);
        return angle < effectiveFov * 0.5f;
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

    // ===== 커맨더 사망 패널티 (TankCrewManager에서 호출) =====

    public void SetCommanderDead(bool dead)
    {
        isCommanderDead = dead;
        Debug.Log($"[TankAI] 커맨더 사망={dead} → 시야 패널티 {(dead ? "적용" : "해제")}");
    }

    private bool IsTurretAimed() => gunner.IsAimed(5f);

    private void Shoot() => gunner.Fire();
}
