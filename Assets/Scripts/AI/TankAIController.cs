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
    [SerializeField] private TankCrewManagerBase crewManager;

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private bool loopWaypoints = true;

    [Header("Detection")]
    [SerializeField] private LayerMask occluderMask;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Detection Penalty")]
    [SerializeField, Range(0f, 1f)] private float commanderDeadRangeMul = 0.5f;
    [SerializeField, Range(0f, 1f)] private float commanderDeadFovMul = 0.6f;
    private bool isCommanderDead;

    [Header("Suspicion Reaction")]
    [SerializeField] private bool useSuspicionReaction = true;
    [SerializeField] private float impactReactionRadius = 40f;
    [SerializeField] private float impactReactionDuration = 4f;
    [SerializeField] private float suspicionAimHeight = 1.2f;
    [SerializeField] private float shellReactionCooldown = 0.75f;

    [Header("Retreat")]
    [SerializeField] private float reverseTime = 3f;
    [SerializeField] private float retreatDistance = 40f;

    [Header("Combat Phase")]
    [SerializeField] private float snipingDuration = 5f;
    [SerializeField] private float advanceDuration = 3f;

    private enum RetreatPhase { Reversing, Fleeing }
    private enum CombatPhase { Sniping, Advancing }

    private RetreatPhase retreatPhase;
    private CombatPhase combatPhase = CombatPhase.Sniping;
    private State currentState = State.Patrol;

    private float reverseTimer;
    private Vector3 retreatDestination;
    private bool hasRetreatDestination;

    private float phaseTimer;
    private float reactionTimer;
    private bool isReacting;

    private int waypointIndex;
    private float patrolWaitTimer;
    private bool isWaiting;
    private bool isActive;

    private Vector3 suspiciousPoint;
    private float suspiciousUntil = -999f;
    private int lastNearbyShellId = -1;
    private float lastNearbyShellReactTime = -999f;

    private void Awake()
    {
        if (player == null)
            Debug.LogWarning("[TankAI] Player object missing.");

        if (crewManager != null && profile != null)
            crewManager.SetSwapDelay(profile.crewSwapDelay);

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("[TankAI] No waypoints. Staying alert in place.");
            isActive = true;
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
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.Combat:
                UpdateCombat();
                break;
            case State.Retreat:
                UpdateRetreat();
                break;
        }
    }

    private void UpdatePatrol()
    {
        if (CanDetectPlayer())
        {
            ClearSuspicion();
            ChangeState(State.Combat);
            return;
        }

        UpdateSuspicionAim();

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
            Debug.Log($"[TankAI] Reached waypoint {waypointIndex}, waiting.");
        }
    }

    private void UpdateCombat()
    {
        if (!CanSeePlayer())
        {
            isReacting = false;
            ChangeState(State.Patrol);
            return;
        }

        ClearSuspicion();

        if (!isReacting)
        {
            isReacting = true;
            reactionTimer = profile.reactionTime;
            driver.Stop();
            Debug.Log("[TankAI] Reaction delay start");
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

        AimTurretAtPlayer();

        ITankLoader loaderFunc = loader as ITankLoader;
        if (loaderFunc.GetIsLoaded() && IsTurretAimed())
            Shoot();
    }

    private void UpdateRetreat()
    {
        if (!gunner.IsGunnerDead())
            AimTurretAtPlayer();

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

    private void GoToWaypoint(int index)
    {
        if (waypoints == null || waypoints.Length == 0) return;

        waypointIndex = index % waypoints.Length;
        driver.SetDestination(waypoints[waypointIndex].position);
        Debug.Log($"[TankAI] Moving to waypoint {waypointIndex}");
    }

    private void GoToNextWaypoint()
    {
        if (!loopWaypoints && waypointIndex >= waypoints.Length - 1)
        {
            driver.Stop();
            return;
        }

        GoToWaypoint((waypointIndex + 1) % waypoints.Length);
    }

    private bool CanSeePlayer()
    {
        if (player == null)
        {
            Debug.Log("[TankAI] player is null");
            return false;
        }

        Vector3 eyePos = transform.position + eyeOffset;
        Vector3 toPlayer = player.position - eyePos;
        float dist = toPlayer.magnitude;

        float effectiveRange = profile.detectionRange * (isCommanderDead ? commanderDeadRangeMul : 1f);
        if (dist > effectiveRange) return false;

        if (Physics.Raycast(eyePos, toPlayer.normalized, out var hit, dist, occluderMask))
        {
            Debug.DrawLine(eyePos, hit.point, Color.red);
            Debug.Log($"[TankAI] Blocked by {hit.collider.gameObject.name}");
            return false;
        }

        Debug.DrawLine(eyePos, player.position, Color.green);
        return true;
    }

    private bool CanDetectPlayer()
    {
        if (!CanSeePlayer()) return false;

        Vector3 toPlayer = player.position - transform.position;
        Transform facingTransform = turret != null ? turret : transform;
        float angle = Vector3.Angle(facingTransform.forward, toPlayer);
        float effectiveFov = profile.fieldOfView * (isCommanderDead ? commanderDeadFovMul : 1f);
        return angle < effectiveFov * 0.5f;
    }

    private void AimTurretAtPlayer()
    {
        Vector3 targetPos = player.position;

        if (profile.useLeadTarget)
        {
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            float dist = Vector3.Distance(turret.position, player.position);
            float tof = dist / 800f;
            if (playerRb != null) targetPos += playerRb.velocity * tof;
        }

        ShellData shellData = (loader as ITankLoader).GetLoadedShell();
        gunner.SetAimTarget(targetPos, shellData);
    }

    private void UpdateSuspicionAim()
    {
        if (!useSuspicionReaction) return;
        if (Time.time > suspiciousUntil) return;
        if (gunner == null || gunner.IsGunnerDead()) return;

        Vector3 aimPos = suspiciousPoint + Vector3.up * suspicionAimHeight;
        ShellData shellData = (loader as ITankLoader)?.GetLoadedShell();
       // Debug.Log($"[AI Aim] {name} suspicious aim -> {aimPos} until={suspiciousUntil:0.00} now={Time.time:0.00}");
        gunner.SetAimTarget(aimPos, shellData);
    }

    private void RegisterNearbyShellSuspicion(Vector3 worldPoint)
    {
        if (!useSuspicionReaction)
        {
            Debug.Log($"[AI Suspicion] {name} skipped: useSuspicionReaction off");
            return;
        }
        if (gunner == null || gunner.IsGunnerDead())
        {
            Debug.Log($"[AI Suspicion] {name} skipped: gunner unavailable");
            return;
        }
        if (currentState == State.Combat)
        {
            Debug.Log($"[AI Suspicion] {name} skipped: already in combat");
            return;
        }

        suspiciousPoint = worldPoint;
        suspiciousUntil = Mathf.Max(suspiciousUntil, Time.time + impactReactionDuration);
       // Debug.Log($"[AI Suspicion] {name} registered point={suspiciousPoint} until={suspiciousUntil:0.00}");
    }

    private void ClearSuspicion()
    {
        suspiciousUntil = -999f;
    }

    public void OnNearbyShell(int shellId, Vector3 shellWorldPos)
    {
        //Debug.Log($"[AI React] {name} received shell={shellId} pos={shellWorldPos}");

        if (!useSuspicionReaction)
        {
            Debug.Log($"[AI React] {name} ignored: suspicion reaction off");
            return;
        }
        if (currentState == State.Combat)
        {
            Debug.Log($"[AI React] {name} ignored: combat state");
            return;
        }
        if (CanDetectPlayer())
        {
            Debug.Log($"[AI React] {name} ignored: can already detect player");
            return;
        }

        float now = Time.time;
        if (lastNearbyShellId == shellId && now - lastNearbyShellReactTime < shellReactionCooldown)
        {
            Debug.Log($"[AI React] {name} ignored: cooldown");
            return;
        }

        lastNearbyShellId = shellId;
        lastNearbyShellReactTime = now;

        RegisterNearbyShellSuspicion(shellWorldPos);
    }

    private bool ShouldRetreat()
    {
        if (crewManager == null) return false;

        bool gunBroken = !gunner.IsGunEquipmentOk;
        bool gunnerLost = !crewManager.IsGunnerAvailable();
        bool canRetreat = crewManager.IsDriverAvailable();
        return canRetreat && (gunBroken || gunnerLost);
    }

    private void UpdateReversing()
    {
        reverseTimer -= Time.deltaTime;

        if (player != null)
        {
            Vector3 awayDir = (transform.position - player.position).normalized;
            Vector3 reverseTarget = transform.position + awayDir * 5f;
            driver.SetReverseDestination(reverseTarget);
        }

        if (reverseTimer <= 0f)
        {
            retreatPhase = RetreatPhase.Fleeing;
            TrySetFleeDestination();
            Debug.Log("[TankAI] Reverse done -> flee");
        }
    }

    private void UpdateFleeing()
    {
        if (!hasRetreatDestination)
        {
            if (!TrySetFleeDestination())
            {
                driver.Stop();
                return;
            }
        }

        if (driver.IsArrived())
        {
            driver.Stop();
            Debug.Log("[TankAI] Retreat complete");
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
            Debug.Log($"[TankAI] Retreat destination: {retreatDestination}");
            return true;
        }

        for (int i = 1; i < 8; i++)
        {
            Vector3 rotDir = Quaternion.Euler(0f, i * 45f, 0f) * awayDir;
            Vector3 rotCandidate = transform.position + rotDir * retreatDistance;

            if (NavMesh.SamplePosition(rotCandidate, out var rotHit, retreatDistance * 0.5f, NavMesh.AllAreas))
            {
                retreatDestination = rotHit.position;
                hasRetreatDestination = true;
                Debug.Log($"[TankAI] Retreat destination retry: {retreatDestination}");
                return true;
            }
        }

        Debug.LogWarning("[TankAI] Could not find retreat destination.");
        return false;
    }

    private void ChangeState(State next)
    {
        Debug.Log($"[TankAI] {currentState} -> {next}");
        currentState = next;

        if (next == State.Combat)
        {
            (loader as ITankLoader).Load(AmmoType.AP);
            combatPhase = CombatPhase.Sniping;
            phaseTimer = snipingDuration;
            isReacting = false;
        }
        else if (next == State.Retreat)
        {
            retreatPhase = RetreatPhase.Reversing;
            reverseTimer = reverseTime;
            hasRetreatDestination = false;
            driver.Stop();
            Debug.Log("[TankAI] Retreat start -> reverse phase");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (profile == null) return;

        float effectiveRange = profile.detectionRange * (isCommanderDead ? commanderDeadRangeMul : 1f);
        float effectiveFov = profile.fieldOfView * (isCommanderDead ? commanderDeadFovMul : 1f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, effectiveRange);

        Vector3 leftDir = Quaternion.Euler(0f, -effectiveFov * 0.5f, 0f) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0f, effectiveFov * 0.5f, 0f) * transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, leftDir * effectiveRange);
        Gizmos.DrawRay(transform.position, rightDir * effectiveRange);

        if (hasRetreatDestination)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(retreatDestination, 1f);
            Gizmos.DrawLine(transform.position, retreatDestination);
        }

        if (useSuspicionReaction && Time.time <= suspiciousUntil)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);
            Gizmos.DrawSphere(suspiciousPoint + Vector3.up * suspicionAimHeight, 0.6f);
            Gizmos.DrawLine(transform.position + eyeOffset, suspiciousPoint + Vector3.up * suspicionAimHeight);
            Gizmos.DrawWireSphere(transform.position, impactReactionRadius);
        }

        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.white;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.4f);
            Gizmos.DrawLine(waypoints[i].position, waypoints[(i + 1) % waypoints.Length].position);
        }
    }

    public void Die()
    {
        enabled = false;
        driver.SetDriverDead();
        gunner.SetGunnerDead();
        Debug.LogWarning($"[TankAI] {gameObject.name} dead -> AI stopped");
    }

    public void SetCommanderDead(bool dead)
    {
        isCommanderDead = dead;
        Debug.Log($"[TankAI] Commander dead={dead} -> detection penalty {(dead ? "on" : "off")}");
    }

    private bool IsTurretAimed() => gunner.IsAimed(5f);

    private void Shoot() => gunner.Fire();
}
