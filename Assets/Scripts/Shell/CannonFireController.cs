using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonFireController : FireController
{
    protected override bool IsPlayerShell => true;

    [Header("Refs")]
    [SerializeField] private CameraController cameraShotShake;

    [SerializeField, Range(0f, 2f)] private float cameraShakeIntensity = 1.0f;
    [SerializeField, Range(0f, 2f)] private float turretShakeIntensity = 0.35f;

    [Header("Gun Recoil Visual")]
    [SerializeField] private Transform recoilPart;
    [SerializeField] private Vector3 recoilLocalAxis = new Vector3(0f, 0f, -1f);
    [SerializeField] private float recoilDistance = 0.18f;
    [SerializeField] private float recoilKickSpeed = 14f;
    [SerializeField] private float recoilReturnSpeed = 6f;
    [SerializeField] private float recoilHoldTime = 0.03f;

    private Vector3 recoilBaseLocalPos;
    private float recoilCur;
    private float recoilTarget;
    private float recoilHoldTimer;

    private void Awake()
    {
        if (recoilPart != null)
            recoilBaseLocalPos = recoilPart.localPosition;
    }

    private void Update()
    {
        UpdateGunRecoil(Time.deltaTime);
    }

    private void UpdateGunRecoil(float dt)
    {
        if (recoilPart == null) return;

        if (recoilHoldTimer > 0f)
        {
            recoilHoldTimer -= dt;
            ApplyRecoilVisual();
            return;
        }

        float speed = (recoilTarget > recoilCur) ? recoilKickSpeed : recoilReturnSpeed;
        recoilCur = Mathf.MoveTowards(recoilCur, recoilTarget, speed * dt);

        if (Mathf.Approximately(recoilCur, recoilTarget) && recoilTarget > 0f)
        {
            recoilHoldTimer = recoilHoldTime;
            recoilTarget = 0f;
        }

        ApplyRecoilVisual();
    }

    private void TriggerGunRecoil()
    {
        if (recoilPart == null) return;
        recoilTarget = Mathf.Max(recoilTarget, recoilDistance);
    }

    private void ApplyRecoilVisual()
    {
        Vector3 axis = recoilLocalAxis.sqrMagnitude > 1e-6f ? recoilLocalAxis.normalized : Vector3.back;
        recoilPart.localPosition = recoilBaseLocalPos + axis * recoilCur;
    }

    private void TriggerShotShake()
    {
        if (cameraShotShake) cameraShotShake.TriggerShake(cameraShakeIntensity);
    }

    public sealed override void FireProjectile(Vector3 dir, AmmoType type)
    {
        base.FireProjectile(dir, type);
        TriggerShotShake();
        TriggerGunRecoil();
    }
}
