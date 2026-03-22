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

        // ��ž Yaw ����
        AimAtWorldPoint(aimTargetPos);

        shell = Shell;
        if (shell != null)
            ApplyFcsToWorldPoint();

        //���� ����
        DriveGunPitchToTarget();
    }

    public void SetAimTarget(Vector3 worldPos, ShellData shellData)
    {
        aimTargetPos = worldPos;
        shell = shellData;
        isAiming = true;
    }

    // 조준 완료 판정 — Yaw + Pitch 둘 다 확인
    public bool IsAimed(float thresholdDeg = 2f)
    {
        // Yaw (수평) 체크
        Vector3 toTarget = (aimTargetPos - turretYaw.position);
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return false;
        float yawError = Vector3.Angle(turretYaw.forward, toTarget);
        if (yawError >= thresholdDeg) return false;

        // Pitch (앙각) 체크 — 현재 각도가 목표에 충분히 가까운지
        float curPitch  = NormalizeAngle(gunPitch.localEulerAngles.x);
        float pitchError = Mathf.Abs(Mathf.DeltaAngle(curPitch, pitchTargetLocalX));
        return pitchError < thresholdDeg;
    }

    public override void Fire()
    {
        if (!CanFire)
        {
            Debug.Log("[AIGunner]��� �Ұ�");
            return;
        }

        if (!loaderFunc.GetIsLoaded() || loaderFunc.GetIsLoading())
        {
            Debug.Log("[AIGunner] ���� �ȵ�");
            return;
        }
    
        Vector3 shotDir = GetDispersionShotDirection();
        AmmoType type = loaderFunc.GetLoadedAmmoType();
        fireController.FireProjectile(shotDir, type);
        soundController.PlayGunFireClips();

        loaderFunc.IsShot();
        loaderFunc.LoadDefault(); // �߻� �� �ڵ� ������
    }

    private void UpdateRange()
    {
        // Ÿ�ٱ��� ���� �Ÿ��� �ڵ����� ��Ÿ��� ����
        Vector3 flat = Vector3.ProjectOnPlane(
            aimTargetPos - gunPitch.position, Vector3.up);
        rangeMeters = Mathf.Clamp(flat.magnitude, 5f, 5000f);
    }
    public override void ApplyFcsToWorldPoint()
    {
        if (shell == null) return;

        // ���� �Ÿ��� ��Ÿ��� �ڵ� ����
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
