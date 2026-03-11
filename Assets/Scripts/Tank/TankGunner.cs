using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class TankGunner : MonoBehaviour
{
    [SerializeField] protected Transform turretYaw;
    [SerializeField] protected Transform gunPitch;
    protected LoaderController loader;
    protected SoundController soundController;
    protected ITankLoader loaderFunc;

    [Header("Aim Settings")]
    [SerializeField] protected float yawSpeedDeg = 120f;
    [SerializeField] protected float pitchSpeedDeg = 90f;
    [SerializeField] protected Vector2 pitchLimits = new Vector2(-10f, 20f);
    [SerializeField] protected float fcsSolveMaxTime = 8.0f;
    [SerializeField] protected float fcsDt = 0.01f;
    [SerializeField] protected int fcsIterations = 18;

    protected float pitchTargetLocalX = 0f;
    protected float rangeMeters = 200f;
    protected ShellData shell;

    [SerializeField] private bool gunDestroyed;
    [SerializeField] private bool breechDestroyed;
    [SerializeField] private bool gunnerDead;

    [Header("Dispersion")]
    [SerializeField] protected bool useDispersion = true;

    // 거리별 분산(도) 선형 보간
    [SerializeField] protected float dispersionAt100mDeg = 0.01f;
    [SerializeField] protected float dispersionAt2000mDeg = 0.1f;

    [Header("Dispersion Penalty")]
    [SerializeField] protected float maxGunnerDispersionPenalty = 1.8f; // 거너 상태 나쁠 때 최대
    [SerializeField] protected float maxGunDispersionPenalty = 2.2f;    // 포신 상태 나쁠 때 최대

    [SerializeField, Range(0f, 1f)] protected float gunHpRatio = 1f;    // 브릿지에서 넣어줄 값


    protected float prevTurretYaw;
    protected float prevGunPitch;

    public void SetGunDestroyed() => gunDestroyed = true;
    public void SetBreechDestroyed() => breechDestroyed = true;
    public void SetGunnerDead() => gunnerDead = true;
    public bool IsGunnerDead() => gunnerDead;
    public bool CanFire => !gunDestroyed && !breechDestroyed && !gunnerDead;

    public bool IsGunEquipmentOk => !gunDestroyed && !breechDestroyed;

    [SerializeField, Range(0f, 1f)] protected float gunnerMul = 1f; // 회전/조절 속도 배율


    protected virtual void Awake()
    {
        loader = GetComponent<LoaderController>();
        soundController = GetComponent<SoundController>();
        loaderFunc = loader as ITankLoader;
    }

    // ===== 공통 함수 =====

    protected void AimAtWorldPoint(Vector3 worldPoint)
    {
        Vector3 to = worldPoint - turretYaw.position;
        Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f) return;

        Quaternion targetYaw = Quaternion.LookRotation(flat, Vector3.up);
        turretYaw.rotation = Quaternion.RotateTowards(
            turretYaw.rotation, targetYaw, yawSpeedDeg * Time.deltaTime * gunnerMul);
    }

    protected void DriveGunPitchToTarget()
    {
        float cur = NormalizeAngle(gunPitch.localEulerAngles.x);
        float next = Mathf.MoveTowardsAngle(
            cur, pitchTargetLocalX, pitchSpeedDeg * Time.deltaTime * gunnerMul);
        var e = gunPitch.localEulerAngles;
        e.x = next;
        gunPitch.localEulerAngles = e;
    }
  
    protected Vector3 GetDispersionShotDirection()
    {
        Vector3 baseDir = gunPitch.forward; 

        if (!useDispersion) return baseDir.normalized;

        //거리 기반 기본 분산
        float t = Mathf.InverseLerp(100f, 1000f, rangeMeters);
        float baseDispDeg = Mathf.Lerp(dispersionAt100mDeg, dispersionAt2000mDeg, t);

        //거너 체력 페널티 (HP 낮을수록 분산 증가)
        float gunnerHp01 = Mathf.InverseLerp(0.45f, 1f, gunnerMul); 
        float gunnerPenalty = Mathf.Lerp(maxGunnerDispersionPenalty, 1f, gunnerHp01);

        //포신 체력 페널티
        float gunPenalty = Mathf.Lerp(maxGunDispersionPenalty, 1f, gunHpRatio);
        float finalDispDeg = baseDispDeg * gunnerPenalty * gunPenalty;

        //원형 분산
        Vector2 rnd = Random.insideUnitCircle * finalDispDeg;
        float yawOffset = rnd.x;
        float pitchOffset = rnd.y;

        Quaternion spreadRot =
            Quaternion.AngleAxis(yawOffset, gunPitch.up) *
            Quaternion.AngleAxis(pitchOffset, gunPitch.right);

        return (spreadRot * baseDir).normalized;
    }

    // ===== FCS 솔버 =====
    protected float SimulateRangeForPitch(float pitchDeg, Vector3 yawForward, float targetHeightDelta = 0f)
    {
        Vector3 startPos = gunPitch.position;

        Vector3 flatYaw = Vector3.ProjectOnPlane(yawForward, Vector3.up);
        if (flatYaw.sqrMagnitude < 1e-6f) return -9999f;
        flatYaw.Normalize();

        Quaternion yawRot = Quaternion.LookRotation(flatYaw, Vector3.up);
        Vector3 pitchAxis = yawRot * Vector3.right;
        Quaternion rot = Quaternion.AngleAxis(-pitchDeg, pitchAxis) * yawRot;
        Vector3 dir = (rot * Vector3.forward).normalized;

        Vector3 velocity = dir * shell.muzzleVelocity;
        Vector3 pos = startPos;

        float airDensity = 1.225f;
        float invMass = 1f / Mathf.Max(1e-6f, shell.projectileMass);
        float r = Mathf.Max(1e-6f, shell.caliber * 0.001f) * 0.5f;
        float refArea = Mathf.PI * r * r * shell.refAreaScale;
        float k = 0.5f * airDensity * shell.dragCoeff * refArea * invMass;

        float t = 0f;
        while (t < fcsSolveMaxTime)
        {
            Vector3 vRel = velocity;
            float spd = vRel.magnitude + 1e-6f;
            Vector3 accel = Physics.gravity + (-k * vRel * spd);

            velocity += accel * fcsDt;
            Vector3 prev = pos;
            pos += velocity * fcsDt;

            float traveled = Vector3.ProjectOnPlane(pos - startPos, Vector3.up).magnitude;
            if (traveled >= rangeMeters)
            {
                float prevTraveled = Vector3.ProjectOnPlane(prev - startPos, Vector3.up).magnitude;
                float u = Mathf.InverseLerp(prevTraveled, traveled, rangeMeters);
                float yAtRange = Mathf.Lerp(prev.y, pos.y, u);
                return yAtRange - (startPos.y + targetHeightDelta);
            }

            if (pos.y < startPos.y - 200f) break;
            t += fcsDt;
        }

        return -9999f;
    }

    // ===== 유틸 =====

    protected static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }
    public void SetGunnerState(bool dead, float hpRatio)
    {
        gunnerDead = dead;
        gunnerMul = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(hpRatio)); // 최소 45%까지
    }
    public void SetGunHpRatio(float hpRatio)
    {
        gunHpRatio = Mathf.Clamp01(hpRatio);
    }

    protected void UpdateTurretSound()
    {
        if (soundController == null) return;

        // 포탑 Yaw 회전량 체크
        float yawDelta = Mathf.Abs(Mathf.DeltaAngle(
        prevTurretYaw, turretYaw.localEulerAngles.y)); 

        // 포신 Pitch 회전량 체크
        float pitchDelta = Mathf.Abs(Mathf.DeltaAngle(
            prevGunPitch, gunPitch.localEulerAngles.x));

        bool isMoving = (yawDelta + pitchDelta) > 0.01f;
        soundController.IsTurretMoving(isMoving);

        prevTurretYaw = turretYaw.localEulerAngles.y; 
        prevGunPitch = gunPitch.localEulerAngles.x;
    }
    /// <summary>
    /// 추상 함수
    /// </summary>
    public abstract void Fire();
    public abstract void ApplyFcsToWorldPoint();
    public abstract bool TrySolvePitchForRange(out float solvedPitchDeg);
}
