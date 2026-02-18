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

public class GunnerController : MonoBehaviour, ITankGunner
{
    [Header("Refs")]
    [SerializeField] private Camera commanderCam;
    [SerializeField] private Transform hull;
    [SerializeField] private Transform turretYaw;
    [SerializeField] private Transform gunPitch;
    [SerializeField] private LayerMask trackableMask;
    private LoaderController loader;
    private CannonFireController fireController;
    private ShellData shell;
    private ITankLoader loaderFunc;

    [Header("Aim")]
    [SerializeField] private float fcsSolveMaxTime = 8.0f;   // 시뮬 최대 시간
    [SerializeField] private float fcsDt = 0.01f;            // 시뮬 스텝
    [SerializeField] private int fcsIterations = 18;         // 이분탐색 반복
    [SerializeField] private bool fcsHighArc = false;        // 고각/저각 선택
    private Vector3? targetPoint;
    private LayerMask aimMask = ~0;
    private float maxAimDistance = 5000f;
    [SerializeField] private float rangeMeters = 800f;
    private bool isAiming;
    private Vector3 aimPoint;
    private bool isAligning;

    [SerializeField] private float yawSpeedDeg = 120f;
    [SerializeField] private float pitchSpeedDeg = 90f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-10f, 20f);

    [Header("Range Input")]
    [SerializeField] private float rangeStep = 5f;          // 한 번에 5m
    [SerializeField] private float repeatDelay = 0.35f;      // 누르고 있을 때 처음 반복까지 딜레이
    [SerializeField] private float repeatRate = 0.08f;       // 반복 간격(초)
    private float _rangeRepeatTimer = 0f;
    private int _rangeRepeatDir = 0; // +1 up, -1 down, 0 none
    private float _pitchTargetLocalX = 0f;

    [Header("Tracking (No Rigidbody)")]
    [SerializeField] private bool tracking = false;
    [SerializeField] private Transform trackingTarget;
    private Transform designatedTarget;
    [SerializeField] private float velSmoothing = 12f;   // 속도 스무딩(8~16)
    [SerializeField] private int leadIterations = 4;     // 예측 반복(3~5)
    [SerializeField] private float maxLeadTime = 6f;     // 예측 최대 시간 제한

    private Vector3 _prevTargetPos;
    private bool _hasPrevTarget;
    private Vector3 _targetVelSmoothed;

    private void Awake()
    {
        loader = GetComponent<LoaderController>();
        fireController = GetComponent<CannonFireController>();


        loaderFunc = loader as ITankLoader;
    }

    private void Start()
    {
        SetRange(rangeMeters);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var ray = commanderCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;

                int hitLayerBit = 1 << hit.collider.gameObject.layer;
                if ((trackableMask.value & hitLayerBit) != 0)
                {
                    designatedTarget = hit.collider.transform;
                    Debug.Log($"[Designator] TRACK target={designatedTarget.name}, point={hit.point}");
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

        HandleRangeHotkeys();
        // ===== 실행(조준 추적) =====
        if (isAligning)
        {
            if (AlignHullStep()) isAligning = false;
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
    }
    private void HandleRangeHotkeys()
    {

        if (Input.GetKeyDown(KeyCode.Alpha1)) Aim();


        if (Input.GetKeyDown(KeyCode.Alpha2)) Fire();


        if (Input.GetKeyDown(KeyCode.T))
        {
            CeaseAction();
        }

        // 1) 첫 입력(탭) 처리
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            SetRange(rangeMeters + rangeStep);
            _rangeRepeatDir = +1;
            _rangeRepeatTimer = repeatDelay;
            Debug.Log($"[Gunner] 사거리 -> {targetPoint}");
            return;
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            SetRange(rangeMeters - rangeStep);
            _rangeRepeatDir = -1;
            _rangeRepeatTimer = repeatDelay;
            Debug.Log($"[Gunner] 사거리 -> {targetPoint}");
            return;
        }

        // 2) 키를 뗐으면 반복 중지
        bool holdingUp = Input.GetKey(KeyCode.UpArrow);
        bool holdingDown = Input.GetKey(KeyCode.DownArrow);

        if (!holdingUp && !holdingDown)
        {
            _rangeRepeatDir = 0;
            return;
        }

        // 3) 둘 다 누르면(충돌) 중지
        if (holdingUp && holdingDown)
        {
            _rangeRepeatDir = 0;
            return;
        }

        // 4) 홀드 반복 처리
        int dir = holdingUp ? +1 : -1;

        // 눌렀던 방향이 바뀌면 딜레이 리셋
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
            Debug.LogWarning("[Gunner] 저장된 지점이 없어. 먼저 클릭으로 지점 지정해줘.");
            return;
        }

        // 1) 트래킹 가능한 타겟이 찍혀있으면 트래킹
        if (designatedTarget != null)
        {
            trackingTarget = designatedTarget;
            StartTracking();
            isAiming = false;
            Debug.Log($"[Gunner] 트래킹 에임 시작 -> {trackingTarget.name}");
            return;
        }

        // 2) 아니면 고정점 에임(땅 포함)
        StopTracking();
        isAiming = true;
        aimPoint = targetPoint.Value;
        Debug.Log($"[Gunner] 고정 포인트 에임 -> {aimPoint}");
    }

    public void AlignHull()
    {
        isAiming = false;
        isAligning = true;

        Debug.Log("[Gunner] 포신 정렬!");
    }
    private bool AlignHullStep()
    {
        //차체 yaw
        float targetYaw = hull.eulerAngles.y;

        // 현재 turret yaw를 목표로 조금씩 이동
        var y = turretYaw.eulerAngles;
        y.y = Mathf.MoveTowardsAngle(y.y, targetYaw, yawSpeedDeg * Time.deltaTime);
        turretYaw.eulerAngles = y;

        // pitch는 0도로 조금씩 이동 (local)
        float curPitch = NormalizeAngle(gunPitch.localEulerAngles.x);
        float nextPitch = Mathf.MoveTowardsAngle(curPitch, 0f, pitchSpeedDeg * Time.deltaTime);
        var p = gunPitch.localEulerAngles;
        p.x = nextPitch;
        gunPitch.localEulerAngles = p;

        // 완료 판정(각도 차이 거의 없으면 종료)
        bool yawDone = Mathf.Abs(Mathf.DeltaAngle(y.y, targetYaw)) < 0.5f;
        bool pitchDone = Mathf.Abs(Mathf.DeltaAngle(nextPitch, 0f)) < 0.5f;

        return yawDone && pitchDone;
    }
    public void CeaseAction()
    {
        isAiming = false;
        isAligning = false;
        designatedTarget = null;
        targetPoint = null;
        StopTracking();
        Debug.Log("[Gunner] 사격 대기!");
    }

    public void SetRange(float meters)
    {
        if (float.IsNaN(meters) || float.IsInfinity(meters))
        {
            Debug.LogWarning("[Gunner] 사거리 입력이 유효하지 않아 무시합니다.");
            return;
        }

        float clamped = Mathf.Clamp(meters, 5f, maxAimDistance);
        if (Mathf.Abs(clamped - rangeMeters) < 0.01f) return;

        rangeMeters = clamped;
        Debug.Log($"[Gunner] 사거리 = {rangeMeters:0}m");

        var loaded = loaderFunc.GetLoadedShell();
        if (loaded == null)
        {
            Debug.LogWarning("[Gunner] 장전된 탄이 없어 FCS 계산 불가");
            return;
        }

        shell = loaded;

        if (!TrySolvePitchForRange(out float pitchDeg))
        {
            Debug.LogWarning("[Gunner] FCS 솔브 실패(도달 불가 or 피치 제한)");
            return;
        }

        float targetLocalX = -pitchDeg; // 반대면 +pitchDeg
        targetLocalX = Mathf.Clamp(targetLocalX, pitchLimits.x, pitchLimits.y);

        _pitchTargetLocalX = targetLocalX;     //  여기로 통일
        isAligning = false;     

        Debug.Log($"[Gunner] 고각 목표 -> {pitchDeg:0.00}deg (localX={_pitchTargetLocalX:0.00})");
    
     }

    private void AimAtWorldPoint(Vector3 worldPoint)
    {
        // Yaw는 그대로 (방향 정렬)
        Vector3 to = worldPoint - turretYaw.position;
        Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
        if (flat.sqrMagnitude > 0.0001f)
        {
            Quaternion targetYaw = Quaternion.LookRotation(flat, Vector3.up);
            turretYaw.rotation = Quaternion.RotateTowards(turretYaw.rotation, targetYaw, yawSpeedDeg * Time.deltaTime);
        }

    }
    public void Fire()
    {
        if (!loaderFunc.GetIsLoaded() || loaderFunc.GetIsLoading())
        {
            Debug.Log("[Gunner] 장전이 되지 않았습니다! 사격 불가");
            return;
        }

        Debug.Log("[Gunner] 발사!");
        fireController.FireProjectile();

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

    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }

    private void UpdateTracking(float dt)
    {
        // 1) 목표 속도 추정(위치 미분 + 스무딩)
        Vector3 vel = EstimateTargetVelocity(dt);

        // 2) 예측 지점 계산(리드)
        Vector3 predicted = PredictFutureAimPointIterative(trackingTarget.position, vel);

        // 3) 포탑 yaw는 예측 지점으로
        AimAtWorldPoint(predicted);

        // 4) FCS(사거리+높이차)도 예측 지점 기준으로 갱신해서 포신 pitch 목표각 업데이트
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

        // 지글지글 떨림 방지(저역통과)
        float a = 1f - Mathf.Exp(-velSmoothing * dt);
        _targetVelSmoothed = Vector3.Lerp(_targetVelSmoothed, rawVel, a);

        return _targetVelSmoothed;
    }

    private Vector3 PredictFutureAimPointIterative(Vector3 targetPos, Vector3 targetVel)
    {
        Vector3 predicted = targetPos;

        // 탄 데이터 확보
        var loaded = loaderFunc.GetLoadedShell();
        if (loaded == null) return predicted;
        shell = loaded;

        // 반복 예측
        for (int i = 0; i < leadIterations; i++)
        {

            // 플레이어가 설정한 rangeMeters에 도달하는 시간(대략)을 구함
            // 고각은 "현재 사거리 설정"으로부터 나온 값이 필요함
            if (!TrySolvePitchForRange(out float pitchDeg)) break;

            float tof = EstimateTimeToHorizontalRange(pitchDeg, turretYaw.forward); // 이 함수는 rangeMeters를 사용
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
        float r = Mathf.Max(1e-6f, (shell.caliber * 0.001f)) * 0.5f;
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
    private void ApplyFcsToWorldPoint()
    {
        var loaded = loaderFunc.GetLoadedShell();
        if (loaded == null) return;
        shell = loaded;

        if (!TrySolvePitchForRange(out float pitchDeg))
            return;

        float targetLocalX = -pitchDeg;
        targetLocalX = Mathf.Clamp(targetLocalX, pitchLimits.x, pitchLimits.y);
        _pitchTargetLocalX = targetLocalX;
    }

    private void DriveGunPitchToTarget()
    {
        float cur = NormalizeAngle(gunPitch.localEulerAngles.x);
        float next = Mathf.MoveTowardsAngle(cur, _pitchTargetLocalX, pitchSpeedDeg * Time.deltaTime);

        var e = gunPitch.localEulerAngles;
        e.x = next;
        gunPitch.localEulerAngles = e;

       // Debug.Log($"cur={NormalizeAngle(gunPitch.localEulerAngles.x):0.00} target={_pitchTargetLocalX:0.00}");
    }
    private float GetTargetHeightDelta()
    {
        // aiming이면 클릭한 aimPoint(또는 targetPoint)를 쓰고
        // tracking이면 trackingTarget.position을 쓰는게 자연스러움
        Vector3 targetPos =
            (tracking && trackingTarget != null) ? trackingTarget.position :
            (targetPoint.HasValue ? targetPoint.Value : gunPitch.position);

        return targetPos.y - gunPitch.position.y; // 목표 - 포신 높이
    }

    private float SimulateRangeForPitch(float pitchDeg, Vector3 yawForward, float targetHeightDelta = 0f)
    {
        Vector3 startPos = gunPitch.position;

        // yawForward는 반드시 수평으로!
        Vector3 flatYaw = Vector3.ProjectOnPlane(yawForward, Vector3.up);
        if (flatYaw.sqrMagnitude < 1e-6f) return -9999f;
        flatYaw.Normalize();

        //  yaw 기준 right 축으로 pitch 적용
        Quaternion yawRot = Quaternion.LookRotation(flatYaw, Vector3.up);
        Vector3 pitchAxis = yawRot * Vector3.right;

        Quaternion rot = Quaternion.AngleAxis(-pitchDeg, pitchAxis) * yawRot; // 부호 반대면 +pitchDeg
        Vector3 dir = (rot * Vector3.forward).normalized;

        Vector3 velocity = dir * shell.muzzleVelocity;
        Vector3 pos = startPos;

        // k 계산 (너 BallisticManager랑 동일)
        float airDensity = 1.225f;
        Vector3 windWorld = Vector3.zero;
        float invMass = 1.0f / Mathf.Max(1e-6f, shell.projectileMass);
        float r = Mathf.Max(1e-6f, (shell.caliber * 0.001f)) * 0.5f;
        float refArea = Mathf.PI * r * r * shell.refAreaScale;
        float k = 0.5f * airDensity * shell.dragCoeff * refArea * invMass;

        float t = 0f;

        while (t < fcsSolveMaxTime)
        {
            Vector3 vRel = velocity - windWorld;
            float spd = vRel.magnitude + 1e-6f;
            Vector3 accel = Physics.gravity + (-k * vRel * spd);

            velocity += accel * fcsDt;
            Vector3 prev = pos;
            pos += velocity * fcsDt;

            //  수평 거리 magnitude로 체크
            float traveled = Vector3.ProjectOnPlane(pos - startPos, Vector3.up).magnitude;

            if (traveled >= rangeMeters)
            {
                float prevTraveled = Vector3.ProjectOnPlane(prev - startPos, Vector3.up).magnitude;
                float u = Mathf.InverseLerp(prevTraveled, traveled, rangeMeters);
                float yAtRange = Mathf.Lerp(prev.y, pos.y, u);

                float desiredY = startPos.y + targetHeightDelta;
                return yAtRange - desiredY;
            }

            if (pos.y < startPos.y - 200f) break;
            t += fcsDt;
        }

        return -9999f;
    }

    private bool TrySolvePitchForRange(out float solvedPitchDeg)
    {
        solvedPitchDeg = 0f;

        if (shell == null) return false;

        // yawForward는 현재 포탑이 향하는 방향으로 (혹은 aimDirWorld)
        Vector3 yawForward = turretYaw.forward;

        float heightDelta = GetTargetHeightDelta();

        // 저각/고각 선택에 따라 탐색 범위를 다르게
        float lo = pitchLimits.x;   // -10 같은 값 포함
        float hi = pitchLimits.y;   // +20

        // 양끝 샘플
        float fLo = SimulateRangeForPitch(lo, yawForward, heightDelta);
        float fHi = SimulateRangeForPitch(hi, yawForward, heightDelta);

        //Debug.Log($"[FCS] lo={lo} fLo={fLo:0.000}, hi={hi} fHi={fHi:0.000}, range={rangeMeters}");

        // 부호가 같으면(둘 다 위/아래) 해결 불가일 수 있음
        // 이런 경우는 hi를 더 키워서 다시 시도하거나, 도달 불가로 처리
        if (Mathf.Sign(fLo) == Mathf.Sign(fHi))
        {
            // hi를 조금 더 늘려볼 수도 있음(제한 내에서)
            // 여기선 그냥 실패 처리
            return false;
        }

        for (int i = 0; i < fcsIterations; i++)
        {
            float mid = 0.5f * (lo + hi);
            float fMid = SimulateRangeForPitch(mid, yawForward, heightDelta);

            if (Mathf.Abs(fMid) < 0.02f) // 2cm 높이 오차면 충분히 정확
            {
                solvedPitchDeg = mid;
                return true;
            }

            // 부호가 바뀌는 구간 유지
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

   
}
