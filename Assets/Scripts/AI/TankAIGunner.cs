using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TankAIGunner : TankGunner
{
    [Header("Refs")]
    private AICannonFireController fireController;


    private ShellData Shell => loaderFunc.GetLoadedShell();

    private Vector3 aimTargetPos;
    private bool isAiming = false;

    protected override void Awake()
    {
        base.Awake();
        fireController = GetComponent<AICannonFireController>();
    }

    private void Update()
    {
        if(IsGunnerDead()) return;

        if (!isAiming) return;

        // 포탑 Yaw 조준
        AimAtWorldPoint(aimTargetPos);

        shell = Shell;
        if (shell != null)
            ApplyFcsToWorldPoint();

        //포신 구동
        DriveGunPitchToTarget();
    }

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

    public override void Fire()
    {
        if (!CanFire)
        {
            Debug.Log("[AIGunner]사격 불가");
            return;
        }

        if (!loaderFunc.GetIsLoaded() || loaderFunc.GetIsLoading())
        {
            Debug.Log("[AIGunner] 장전 안됨");
            return;
        }
    
        Vector3 shotDir = GetDispersionShotDirection();
        AmmoType type = loaderFunc.GetLoadedAmmoType();
        fireController.FireProjectile(shotDir, type);

        loaderFunc.IsShot();
        loaderFunc.LoadDefault(); // 발사 후 자동 재장전
    }

    private void UpdateRange()
    {
        // 타겟까지 수평 거리를 자동으로 사거리로 설정
        Vector3 flat = Vector3.ProjectOnPlane(
            aimTargetPos - gunPitch.position, Vector3.up);
        rangeMeters = Mathf.Clamp(flat.magnitude, 5f, 5000f);
    }
    public override void ApplyFcsToWorldPoint()
    {
        if (shell == null) return;

        // 수평 거리를 사거리로 자동 설정
        rangeMeters = Vector3.ProjectOnPlane(
            aimTargetPos - gunPitch.position, Vector3.up).magnitude;
        rangeMeters = Mathf.Clamp(rangeMeters, 5f, 5000f);

        if (!TrySolvePitchForRange(out float pitchDeg)) return;
        pitchTargetLocalX = Mathf.Clamp(-pitchDeg, pitchLimits.x, pitchLimits.y);
    }
    public override bool TrySolvePitchForRange(out float solvedPitchDeg)
    {
        solvedPitchDeg = 0f;
        if (shell == null) return false;

        Vector3 yawForward = turretYaw.forward;
        float heightDelta = aimTargetPos.y - gunPitch.position.y;

        float lo = pitchLimits.x;
        float hi = pitchLimits.y;
        float fLo = SimulateRangeForPitch(lo, yawForward, heightDelta);
        float fHi = SimulateRangeForPitch(hi, yawForward, heightDelta);

        if (Mathf.Sign(fLo) == Mathf.Sign(fHi)) return false;

        for (int i = 0; i < fcsIterations; i++)
        {
            float mid = 0.5f * (lo + hi);
            float fMid = SimulateRangeForPitch(mid, yawForward, heightDelta);

            if (Mathf.Abs(fMid) < 0.02f)
            {
                solvedPitchDeg = mid;
                return true;
            }

            if (Mathf.Sign(fMid) == Mathf.Sign(fLo)) { lo = mid; fLo = fMid; }
            else { hi = mid; fHi = fMid; }
        }

        solvedPitchDeg = 0.5f * (lo + hi);
        return true;
    }
}
