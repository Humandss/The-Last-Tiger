using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UI;

public interface ITankGunner
{
    void Aim();
    void AlignHull();
    void SetRange(float meters);
    void CeaseAction();
    void Fire();
}

public class GunnerController : TankGunner, ITankGunner
{
    [Header("Refs")]
    [SerializeField] private Camera commanderCam;
    [SerializeField] private Transform hull;
    [SerializeField] private LayerMask trackableMask;
    private CannonFireController fireController;

    [Header("Aim")]
    [SerializeField] private bool fcsHighArc = false;
    private Vector3? targetPoint;
    private LayerMask aimMask = ~0;
    private float maxAimDistance = 5000f;
    private bool isAiming;
    private Vector3 aimPoint;
    private bool isAligning;

    [Header("Range Input")]
    [SerializeField] private float rangeStep = 5f;
    [SerializeField] private float repeatDelay = 0.35f;
    [SerializeField] private float repeatRate = 0.08f;
    private float _rangeRepeatTimer = 0f;
    private int _rangeRepeatDir = 0;

    [Header("Tracking")]
    [SerializeField] private bool tracking = false;
    [SerializeField] private Transform trackingTarget;
    private Transform designatedTarget;
    [SerializeField] private float velSmoothing = 12f;
    [SerializeField] private int leadIterations = 4;
    [SerializeField] private float maxLeadTime = 6f;
    private Vector3 _prevTargetPos;
    private bool _hasPrevTarget;
    private Vector3 _targetVelSmoothed;

    protected override void Awake()
    {
        base.Awake();
        fireController = GetComponent<CannonFireController>();
    }

    private void Start()
    {
        SetRange(rangeMeters);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(2))
        {
            Ray ray = commanderCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;

                int hitLayerBit = 1 << hit.collider.gameObject.layer;
                if ((trackableMask.value & hitLayerBit) != 0)
                {
                    designatedTarget = hit.collider.transform;
                    float dist = Vector3.Distance(ray.origin, hit.point);
                    Debug.Log($"[Designator] TRACK target={designatedTarget.name}, point={hit.point}, dist={dist:0.0}m");
                }
                else
                {
                    designatedTarget = null;
                    Debug.Log($"[Designator] POINT only, point={hit.point}");
                }
            }
            else
            {
                designatedTarget = null;
                Debug.Log("[Designator] no hit");
            }
        }

        if (IsGunnerDead())
        {
            Debug.Log("[Gunner] 사망! 포탑 조종 불가");
            isAiming = false;
            isAligning = false;
            StopTracking();
            return;
        }

        HandleRangeHotkeys();

        if (isAligning)
        {
            if (AlignHullStep())
            {
                isAligning = false;
                isAiming = false;
                tracking = false;
                trackingTarget = null;
            }
        }
        else if (tracking && trackingTarget != null)
        {
            UpdateTracking(Time.deltaTime);
        }
        else if (isAiming)
        {
            AimAtWorldPoint(aimPoint);
            ApplyFcsToWorldPoint();
        }

        DriveGunPitchToTarget();
        UpdateTurretSound();
    }

    private void HandleRangeHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.F)) Aim();
        if (Input.GetKeyDown(KeyCode.Mouse0)) Fire();

        if (Input.GetKeyDown(KeyCode.T))
            CeaseAction();

        if (Input.GetKeyDown(KeyCode.Y))
            AlignHull();

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.E))
        {
            SetRange(rangeMeters + rangeStep);
            _rangeRepeatDir = +1;
            _rangeRepeatTimer = repeatDelay;
            Debug.Log($"[Gunner] 사거리 -> {targetPoint}");
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Q))
        {
            SetRange(rangeMeters - rangeStep);
            _rangeRepeatDir = -1;
            _rangeRepeatTimer = repeatDelay;
            Debug.Log($"[Gunner] 사거리 -> {targetPoint}");
            return;
        }

        bool holdingUp = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.E);
        bool holdingDown = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Q);

        if (!holdingUp && !holdingDown)
        {
            _rangeRepeatDir = 0;
            return;
        }

        if (holdingUp && holdingDown)
        {
            _rangeRepeatDir = 0;
            return;
        }

        int dir = holdingUp ? +1 : -1;
        if (dir != _rangeRepeatDir)
        {
            _rangeRepeatDir = dir;
            _rangeRepeatTimer = repeatDelay;
            return;
        }

        _rangeRepeatTimer -= Time.deltaTime;
        if (_rangeRepeatTimer <= 0f)
        {
            SetRange(rangeMeters + dir * rangeStep);
            _rangeRepeatTimer = repeatRate;
        }
    }

    public void Aim()
    {
        if (!targetPoint.HasValue)
        {
            Debug.LogWarning("[Gunner] 지정한 지점이 없어. 먼저 미들클릭으로 지점을 지정해줘");
            return;
        }

        if (designatedTarget != null)
        {
            trackingTarget = designatedTarget;
            StartTracking();
            isAiming = false;
            Debug.Log($"[Gunner] 트래킹 에임 시작 -> {trackingTarget.name}");
            return;
        }

        StopTracking();
        isAiming = true;
        aimPoint = targetPoint.Value;
        Debug.Log($"[Gunner] 고정 사인 에임 -> {aimPoint}");
    }

    public void AlignHull()
    {
        isAiming = false;
        isAligning = true;
        Debug.Log("[Gunner] 차신 정렬!");
    }

    private bool AlignHullStep()
    {
        if (IsGunnerDead())
        {
            isAligning = false;
            return true;
        }

        float targetYaw = hull.eulerAngles.y;
        Vector3 y = turretYaw.eulerAngles;
        y.y = Mathf.MoveTowardsAngle(y.y, targetYaw, yawSpeedDeg * gunnerMul * Time.deltaTime);
        turretYaw.eulerAngles = y;

        float curPitch = NormalizeAngle(gunPitch.localEulerAngles.x);
        float nextPitch = Mathf.MoveTowardsAngle(curPitch, 0f, pitchSpeedDeg * gunnerMul * Time.deltaTime);
        Vector3 p = gunPitch.localEulerAngles;
        p.x = nextPitch;
        gunPitch.localEulerAngles = p;

        return Mathf.Abs(Mathf.DeltaAngle(y.y, targetYaw)) < 0.5f &&
               Mathf.Abs(Mathf.DeltaAngle(nextPitch, 0f)) < 0.5f;
    }

    public void CeaseAction()
    {
        isAiming = false;
        isAligning = false;
        designatedTarget = null;
        targetPoint = null;
        StopTracking();
        Debug.Log("[Gunner] 공격 대기");
    }

    public void SetRange(float meters)
    {
        if (float.IsNaN(meters) || float.IsInfinity(meters))
        {
            Debug.LogWarning("[Gunner] 사거리 입력이 유효하지 않아 무시합니다");
            return;
        }

        float clamped = Mathf.Clamp(meters, 5f, maxAimDistance);
        if (Mathf.Abs(clamped - rangeMeters) < 0.01f) return;

        float prevRange = rangeMeters;
        rangeMeters = clamped;

        ShellData loaded = loaderFunc.GetLoadedShell();
        if (loaded == null)
        {
            rangeMeters = prevRange;
            Debug.LogWarning("[Gunner] 장전된 포탄이 없어 FCS 계산 불가");
            return;
        }

        shell = loaded;

        if (!TrySolvePitchForRange(out float pitchDeg))
        {
            rangeMeters = prevRange;
            Debug.LogWarning("[Gunner] FCS 솔브 실패(도달 불가 or 수치 제한)");
            return;
        }

        float targetLocalX = -pitchDeg;
        targetLocalX = Mathf.Clamp(targetLocalX, pitchLimits.x, pitchLimits.y);

        pitchTargetLocalX = targetLocalX;
        isAligning = false;

        Debug.Log($"[Gunner] 사거리 = {rangeMeters:0}m");
        Debug.Log($"[Gunner] 고각 목표 -> {pitchDeg:0.00}deg (localX={pitchTargetLocalX:0.00})");
    }

    public override void Fire()
    {
        if (!CanFire)
        {
            Debug.Log("[Gunner] 자신 고장! 사격 불가");
            return;
        }

        if (!loaderFunc.GetIsLoaded() || loaderFunc.GetIsLoading())
        {
            Debug.Log("[Gunner] 장전이 되지 않았습니다. 사격 불가");
            return;
        }

        Vector3 shotDir = GetDispersionShotDirection();
        AmmoType loadedAmmo = loaderFunc.GetLoadedAmmoType();
        Debug.Log($"[Gunner] 발사! range={rangeMeters:0}m dir={shotDir}");

        fireController.FireProjectile(shotDir, loadedAmmo);
        soundController.PlayGunFireClips();

        loaderFunc.IsShot();
        loaderFunc.LoadDefault();
    }

    public void StartTracking()
    {
        if (trackingTarget == null)
        {
            Debug.LogWarning("[Gunner] Tracking fail, target lost!");
            tracking = false;
            return;
        }

        tracking = true;
        isAiming = false;
        _hasPrevTarget = false;
        _targetVelSmoothed = Vector3.zero;

        Debug.Log($"[Gunner] Tracking start: {trackingTarget.name}");
    }

    private void StopTracking()
    {
        tracking = false;
        isAiming = false;
        trackingTarget = null;
        _hasPrevTarget = false;
        Debug.Log("[Gunner] Tracking stop");
    }

    private void UpdateTracking(float dt)
    {
        Vector3 vel = EstimateTargetVelocity(dt);
        Vector3 predicted = PredictFutureAimPointIterative(trackingTarget.position, vel);

        AimAtWorldPoint(predicted);
        ApplyFcsToWorldPoint();
    }

    private Vector3 EstimateTargetVelocity(float dt)
    {
        dt = Mathf.Max(1e-5f, dt);

        Vector3 cur = trackingTarget.position;
        Vector3 rawVel;

        if (!_hasPrevTarget)
        {
            _prevTargetPos = cur;
            _hasPrevTarget = true;
            rawVel = Vector3.zero;
        }
        else
        {
            rawVel = (cur - _prevTargetPos) / dt;
            _prevTargetPos = cur;
        }

        float a = 1f - Mathf.Exp(-velSmoothing * dt);
        _targetVelSmoothed = Vector3.Lerp(_targetVelSmoothed, rawVel, a);
        return _targetVelSmoothed;
    }

    private Vector3 PredictFutureAimPointIterative(Vector3 targetPos, Vector3 targetVel)
    {
        Vector3 predicted = targetPos;

        ShellData loaded = loaderFunc.GetLoadedShell();
        if (loaded == null) return predicted;
        shell = loaded;

        for (int i = 0; i < leadIterations; i++)
        {
            if (!TrySolvePitchForRange(out float pitchDeg))
                break;

            float tof = EstimateTimeToHorizontalRange(pitchDeg, turretYaw.forward);
            tof = Mathf.Clamp(tof, 0f, maxLeadTime);
            predicted = targetPos + targetVel * tof;
        }

        return predicted;
    }

    private float EstimateTimeToHorizontalRange(float pitchDeg, Vector3 yawForward)
    {
        Vector3 startPos = gunPitch.position;

        Vector3 flatYaw = Vector3.ProjectOnPlane(yawForward, Vector3.up);
        if (flatYaw.sqrMagnitude < 1e-6f) return maxLeadTime;
        flatYaw.Normalize();

        Quaternion yawRot = Quaternion.LookRotation(flatYaw, Vector3.up);
        Vector3 pitchAxis = yawRot * Vector3.right;

        Quaternion rot = Quaternion.AngleAxis(-pitchDeg, pitchAxis) * yawRot;
        Vector3 dir = (rot * Vector3.forward).normalized;

        Vector3 velocity = dir * shell.muzzleVelocity;
        Vector3 pos = startPos;

        float airDensity = 1.225f;
        Vector3 windWorld = Vector3.zero;
        float invMass = 1f / Mathf.Max(1e-6f, shell.projectileMass);
        float r = Mathf.Max(1e-6f, shell.caliber * 0.001f) * 0.5f;
        float refArea = Mathf.PI * r * r * shell.refAreaScale;
        float k = 0.5f * airDensity * shell.dragCoeff * refArea * invMass;

        float t = 0f;
        while (t < fcsSolveMaxTime)
        {
            Vector3 vRel = velocity - windWorld;
            float spd = vRel.magnitude + 1e-6f;
            Vector3 accel = Physics.gravity + (-k * vRel * spd);

            velocity += accel * fcsDt;
            pos += velocity * fcsDt;

            float traveled = Vector3.ProjectOnPlane(pos - startPos, Vector3.up).magnitude;
            t += fcsDt;

            if (traveled >= rangeMeters) return t;
            if (pos.y < startPos.y - 200f) break;
        }

        return maxLeadTime;
    }

    public override void ApplyFcsToWorldPoint()
    {
        ShellData loaded = loaderFunc.GetLoadedShell();
        if (loaded == null) return;
        shell = loaded;

        if (!TrySolvePitchForRange(out float pitchDeg))
            return;

        float targetLocalX = -pitchDeg;
        targetLocalX = Mathf.Clamp(targetLocalX, pitchLimits.x, pitchLimits.y);
        pitchTargetLocalX = targetLocalX;
    }

    private float GetTargetHeightDelta()
    {
        Vector3 targetPos =
            (tracking && trackingTarget != null) ? trackingTarget.position :
            (targetPoint.HasValue ? targetPoint.Value : gunPitch.position);

        return targetPos.y - gunPitch.position.y;
    }

    public override bool TrySolvePitchForRange(out float solvedPitchDeg)
    {
        solvedPitchDeg = 0f;
        if (shell == null) return false;

        Vector3 yawForward = turretYaw.forward;
        float heightDelta = GetTargetHeightDelta();

        float lo = pitchLimits.x;
        float hi = pitchLimits.y;

        float fLo = SimulateRangeForPitch(lo, yawForward, heightDelta);
        float fHi = SimulateRangeForPitch(hi, yawForward, heightDelta);

        if (Mathf.Sign(fLo) == Mathf.Sign(fHi))
            return false;

        for (int i = 0; i < fcsIterations; i++)
        {
            float mid = 0.5f * (lo + hi);
            float fMid = SimulateRangeForPitch(mid, yawForward, heightDelta);

            if (Mathf.Abs(fMid) < 0.02f)
            {
                solvedPitchDeg = mid;
                return true;
            }

            if (Mathf.Sign(fMid) == Mathf.Sign(fLo))
            {
                lo = mid;
                fLo = fMid;
            }
            else
            {
                hi = mid;
                fHi = fMid;
            }
        }

        solvedPitchDeg = 0.5f * (lo + hi);
        return true;
    }

    public float CurrentRangeMeters => rangeMeters;
    public float MaxRangeMeters => maxAimDistance;
    public float MinRangeMeters => 5f;
}
