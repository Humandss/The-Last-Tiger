using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TankAIGunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform turretYaw;
    [SerializeField] private Transform gunPitch;
    [SerializeField] private AICannonFireController fireControllerObj;
    private ICannonFire fireController;
    [SerializeField] private LoaderController loader; 

    private ShellData Shell => (loader as ITankLoader).GetLoadedShell();

    [Header("Aim Settings")]
    [SerializeField] private float yawSpeedDeg = 120f;
    [SerializeField] private float pitchSpeedDeg = 90f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-10f, 20f);

    // GunnerController에서 그대로 가져온 FCS 변수들
    [SerializeField] private float fcsSolveMaxTime = 8.0f;
    [SerializeField] private float fcsDt = 0.01f;
    [SerializeField] private int fcsIterations = 18;
    private float _pitchTargetLocalX = 0f;
    private float rangeMeters = 800f;
    private ShellData shell;

    // AI가 외부에서 세팅해주는 값
    private Vector3 aimTargetPos;
    private bool isAiming = false;
    private bool gunnerDead = false;

    private void Awake()
    {
        fireController = fireControllerObj as ICannonFire;
        if (fireController == null)
            Debug.LogError("[AIGunner] ICannonFire 구현체가 없습니다!");
    }

    private void Update()
    {
        if(gunnerDead) return;

        if (!isAiming) return;

        // 포탑 Yaw 조준
        AimAtWorldPoint(aimTargetPos);

        // FCS로 포신 Pitch 계산
        UpdateRange();
        ApplyFcsToWorldPoint();

        //포신 구동
        DriveGunPitchToTarget();
    }

    // TankAIController에서 호출
    public void SetAimTarget(Vector3 worldPos, ShellData shellData)
    {
        aimTargetPos = worldPos;
        shell = shellData;
        isAiming = true;
    }

    // 조준 완료 여부 
    public bool IsAimed(float thresholdDeg = 5f)
    {
        Vector3 toTarget = (aimTargetPos - turretYaw.position);
        toTarget.y = 0f;
        float angle = Vector3.Angle(turretYaw.forward, toTarget);
        return angle < thresholdDeg;
    }

    // ===== ITankGunner 구현 =====
    public void Fire()
    {
        ITankLoader loaderFunc = loader as ITankLoader;

        if (!loaderFunc.GetIsLoaded() || loaderFunc.GetIsLoading())
        {
            Debug.Log("[AIGunner] 장전 안됨");
            return;
        }
        Debug.DrawRay(gunPitch.position, gunPitch.forward * 10f, Color.red, 3f);
        Debug.DrawRay(gunPitch.position, gunPitch.up * 10f, Color.green, 3f);
        Debug.DrawRay(gunPitch.position, gunPitch.right * 10f, Color.blue, 3f);
        Vector3 shotDir = gunPitch.forward;
        AmmoType type = loaderFunc.GetLoadedAmmoType();
        fireController.FireProjectile(shotDir, type);

        loaderFunc.IsShot();
        loaderFunc.LoadDefault(); // 발사 후 자동 재장전
    }

    // ===== GunnerController에서 그대로 복사 =====
    private void AimAtWorldPoint(Vector3 worldPoint)
    {
        Vector3 to = worldPoint - turretYaw.position;
        Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f) return;

        Quaternion targetYaw = Quaternion.LookRotation(flat, Vector3.up);
        turretYaw.rotation = Quaternion.RotateTowards(
            turretYaw.rotation, targetYaw, yawSpeedDeg * Time.deltaTime);
    }

    private void UpdateRange()
    {
        // 타겟까지 수평 거리를 자동으로 사거리로 설정
        Vector3 flat = Vector3.ProjectOnPlane(
            aimTargetPos - gunPitch.position, Vector3.up);
        rangeMeters = Mathf.Clamp(flat.magnitude, 5f, 5000f);
    }

    private void ApplyFcsToWorldPoint()
    {
        ShellData shell = Shell;
        if (shell == null) return;
        if (!TrySolvePitchForRange(out float pitchDeg)) return; // ShellData 파라미터 제거

        float targetLocalX = -pitchDeg;
        _pitchTargetLocalX = Mathf.Clamp(targetLocalX, pitchLimits.x, pitchLimits.y);
    }

    private void DriveGunPitchToTarget()
    {
        float cur = NormalizeAngle(gunPitch.localEulerAngles.x);
        float next = Mathf.MoveTowardsAngle(
            cur, _pitchTargetLocalX, pitchSpeedDeg * Time.deltaTime);
        var e = gunPitch.localEulerAngles;
        e.x = next;
        gunPitch.localEulerAngles = e;
    }

    // GunnerController에서 그대로 복사
    private float GetTargetHeightDelta() =>
        aimTargetPos.y - gunPitch.position.y;

    private static float NormalizeAngle(float a)
    {
        a %= 360f; if (a > 180f) a -= 360f; return a;
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
    public void SetGunnerDead()
    {
        gunnerDead = true;
    }
}
