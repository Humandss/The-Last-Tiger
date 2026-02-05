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
    private float rangeMeters = 800f;
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
                Debug.Log($"[Designator] point = {hit.point}");

            }
            else
            {
                // 아무것도 안 맞으면 저장 안 하거나, 전방 maxDistance로 저장(선택)
                Debug.Log("[Designator] no hit");
            }
        }

        HandleRangeHotkeys();

        // ===== 실행(조준 추적) =====
        if (isAligning)
        {
            if (AlignHullStep())isAligning = false;
      
        }
        else if (isAiming) AimAtWorldPoint(aimPoint);

        DriveGunPitchToTarget();
    }
    private void HandleRangeHotkeys()
    {

        if (Input.GetKeyDown(KeyCode.Alpha1)) Aim();


        if (Input.GetKeyDown(KeyCode.Alpha2)) Fire();

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
        if (targetPoint.HasValue)
        {
            isAiming = true;
            aimPoint = targetPoint.Value;
        }
        else
        {
            Debug.LogWarning("[Gunner] 저장된 지점이 없어. 먼저 클릭으로 지점 지정해줘.");
            return;
        }
        Debug.Log($"[Gunner] 에임 포인트 -> {targetPoint}");
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
        Debug.Log("[Gunner] 행동 취소!");
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
        isAligning = false;                    // (선택) 정렬중이면 풀어서 바로 움직이게

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
    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }

    private void DriveGunPitchToTarget()
    {
        float cur = NormalizeAngle(gunPitch.localEulerAngles.x);
        float next = Mathf.MoveTowardsAngle(cur, _pitchTargetLocalX, pitchSpeedDeg * Time.deltaTime);

        var e = gunPitch.localEulerAngles;
        e.x = next;
        gunPitch.localEulerAngles = e;

        //Debug.Log($"[PITCH] cur={cur:0.00} next={next:0.00} target={_pitchTargetLocalX:0.00} actualX={NormalizeAngle(gunPitch.localEulerAngles.x):0.00}");
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

        // 저각/고각 선택에 따라 탐색 범위를 다르게
        float lo = 0.0f;
        float hi = Mathf.Max(1f, pitchLimits.y); // 위로 드는 최대(예: 20도)

        // 만약 너가 더 큰 고각을 허용하고 싶으면 pitchLimits.y를 올려야 함
        // (포물선 고각은 보통 20도 넘어갈 수 있음)

        // 양끝 샘플
        float fLo = SimulateRangeForPitch(lo, yawForward);
        float fHi = SimulateRangeForPitch(hi, yawForward);

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
            float fMid = SimulateRangeForPitch(mid, yawForward);

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
