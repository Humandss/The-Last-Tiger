using UnityEngine;

/// <summary>
/// 포 발사 시 탱크 차체/포신 반동 효과.
/// - 포신: 뒤로 밀렸다가 복귀 (mantlet recoil)
/// - 차체: 발사 반대 방향으로 평행이동 + 살짝 위로 피치업 → 부드럽게 복귀
///
/// </summary>
public class TankRecoil : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("이동/회전시킬 차체 비주얼 (없어도 됨)")]
    [SerializeField] private Transform chassisVisual;
    [Tooltip("뒤로 밀릴 포신 트랜스폼 (없어도 됨). Z+가 포구 방향")]
    [SerializeField] private Transform gunBarrel;

    [Header("Gun Barrel Recoil")]
    [Tooltip("포신이 뒤로 밀리는 거리(m)")]
    [SerializeField] private float gunPushBack = 0.18f;
    [Tooltip("포신 복귀 SmoothDamp 시간 (작을수록 빨리 복귀)")]
    [SerializeField] private float gunReturnTime = 0.35f;

    [Header("Chassis Translation")]
    [Tooltip("차체가 발사 반대 방향으로 밀리는 거리(m)")]
    [SerializeField] private float chassisPushBack = 0.08f;
    [Tooltip("수평으로만 밀림 (위/아래 무시)")]
    [SerializeField] private bool chassisHorizontalOnly = true;
    [Tooltip("차체 위치 복귀 SmoothDamp 시간")]
    [SerializeField] private float chassisPosReturnTime = 0.55f;

    [Header("Chassis Rotation")]
    [Tooltip("차체가 위로 들리는 각도(도)")]
    [SerializeField] private float chassisPitchUp = 1.2f;
    [Tooltip("차체 좌우 살짝 흔들림 (각도)")]
    [SerializeField] private float chassisRollAmount = 0.4f;
    [Tooltip("차체 회전 복귀 SmoothDamp 시간")]
    [SerializeField] private float chassisRotReturnTime = 0.5f;

    [Header("Curve")]
    [Tooltip("발사 직후 ramp-in 시간 — 0이면 즉시 최대 반동, 작은 값이면 부드러운 시작")]
    [SerializeField] private float kickInTime = 0.04f;

    // ── 내부 상태 ─────────────────────────────────────────

    // 포신
    private Vector3 gunBaseLocalPos;
    private float gunZOffset;
    private float gunZVelocity;
    private float gunZTarget;

    // 차체 위치
    private Vector3 chassisBaseLocalPos;
    private Vector3 chassisPosOffset;
    private Vector3 chassisPosVelocity;
    private Vector3 chassisPosTarget;

    // 차체 회전 (pitch, roll만 적용 — yaw는 운전 방향이라 안 건드림)
    private Quaternion chassisBaseLocalRot;
    private Vector2 chassisAngles;
    private Vector2 chassisAnglesVelocity;
    private Vector2 chassisAnglesTarget;

    // kick-in
    private float kickInTimer;
    private bool kickingIn;
    private float gunKickInTarget;
    private Vector3 chassisPosKickInTarget;
    private Vector2 chassisAnglesKickInTarget;

    private void Awake()
    {
        if (gunBarrel != null) gunBaseLocalPos = gunBarrel.localPosition;
        if (chassisVisual != null)
        {
            chassisBaseLocalPos = chassisVisual.localPosition;
            chassisBaseLocalRot = chassisVisual.localRotation;
        }
    }

    /// <summary>
    /// 발사 시 호출. shotDir은 월드 좌표계 발사 방향 (포구 forward).
    /// </summary>
    public void Fire(Vector3 shotDir)
    {
        // 1) 포신 — 로컬 -Z 방향으로 후퇴
        gunKickInTarget = -gunPushBack;

        // 2) 차체 위치 — 발사 반대 방향으로 평행이동
        if (chassisVisual != null && shotDir.sqrMagnitude > 1e-6f)
        {
            // 월드 발사 반대방향 → 차체의 부모 로컬 공간으로 변환
            Transform refSpace = chassisVisual.parent != null ? chassisVisual.parent : chassisVisual;
            Vector3 worldPushDir = -shotDir.normalized;
            Vector3 localPushDir = refSpace.InverseTransformDirection(worldPushDir);

            if (chassisHorizontalOnly)
            {
                localPushDir.y = 0f;
                if (localPushDir.sqrMagnitude > 1e-6f)
                    localPushDir = localPushDir.normalized;
            }

            chassisPosKickInTarget = localPushDir * chassisPushBack;
        }
        else
        {
            chassisPosKickInTarget = Vector3.zero;
        }

        // 3) 차체 회전 — 위로 들림 + 좌우 랜덤 롤
        float rollSign = Random.value < 0.5f ? -1f : 1f;
        chassisAnglesKickInTarget = new Vector2(-chassisPitchUp, chassisRollAmount * rollSign);

        // 즉시 최대 vs 부드러운 ramp-in
        if (kickInTime <= 0f)
        {
            gunZOffset = gunKickInTarget;
            chassisPosOffset = chassisPosKickInTarget;
            chassisAngles = chassisAnglesKickInTarget;
            kickingIn = false;

            gunZTarget = 0f;
            chassisPosTarget = Vector3.zero;
            chassisAnglesTarget = Vector2.zero;
        }
        else
        {
            kickingIn = true;
            kickInTimer = 0f;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (kickingIn)
        {
            kickInTimer += dt;
            float t = Mathf.Clamp01(kickInTimer / kickInTime);

            gunZOffset = Mathf.Lerp(0f, gunKickInTarget, t);
            chassisPosOffset = Vector3.Lerp(Vector3.zero, chassisPosKickInTarget, t);
            chassisAngles = Vector2.Lerp(Vector2.zero, chassisAnglesKickInTarget, t);

            if (t >= 1f)
            {
                kickingIn = false;
                gunZTarget = 0f;
                chassisPosTarget = Vector3.zero;
                chassisAnglesTarget = Vector2.zero;
                gunZVelocity = 0f;
                chassisPosVelocity = Vector3.zero;
                chassisAnglesVelocity = Vector2.zero;
            }
        }
        else
        {
            // SmoothDamp로 부드럽게 0 복귀
            gunZOffset = Mathf.SmoothDamp(gunZOffset, gunZTarget, ref gunZVelocity, gunReturnTime);
            chassisPosOffset = Vector3.SmoothDamp(chassisPosOffset, chassisPosTarget, ref chassisPosVelocity, chassisPosReturnTime);
            chassisAngles.x = Mathf.SmoothDamp(chassisAngles.x, chassisAnglesTarget.x, ref chassisAnglesVelocity.x, chassisRotReturnTime);
            chassisAngles.y = Mathf.SmoothDamp(chassisAngles.y, chassisAnglesTarget.y, ref chassisAnglesVelocity.y, chassisRotReturnTime);
        }

        ApplyTransforms();
    }

    private void ApplyTransforms()
    {
        if (gunBarrel != null)
        {
            Vector3 p = gunBaseLocalPos;
            p.z += gunZOffset;
            gunBarrel.localPosition = p;
        }

        if (chassisVisual != null)
        {
            chassisVisual.localPosition = chassisBaseLocalPos + chassisPosOffset;

            Quaternion offset = Quaternion.Euler(chassisAngles.x, 0f, chassisAngles.y);
            chassisVisual.localRotation = chassisBaseLocalRot * offset;
        }
    }
}
