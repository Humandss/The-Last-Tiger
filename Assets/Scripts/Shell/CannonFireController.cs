using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;


public class CannonFireController : FireController
{
    [Header("Refs")]
    [SerializeField] private CameraController cameraShotShake;

    [SerializeField, Range(0f, 2f)] private float cameraShakeIntensity = 1.0f;
    [SerializeField, Range(0f, 2f)] private float turretShakeIntensity = 0.35f;

    [Header("Gun Recoil Visual")]
    [SerializeField] private Transform recoilPart;            // 뒤로 밀릴 포신 파츠
    [SerializeField] private Vector3 recoilLocalAxis = new Vector3(0f, 0f, -1f); // 로컬축 기준(보통 -Z)
    [SerializeField] private float recoilDistance = 0.18f;   // 후퇴 거리
    [SerializeField] private float recoilKickSpeed = 14f;    // 뒤로 갈 때 속도
    [SerializeField] private float recoilReturnSpeed = 6f;   // 복귀 속도
    [SerializeField] private float recoilHoldTime = 0.03f;   // 끝에서 잠깐 멈춤(타격감)

    private Vector3 recoilBaseLocalPos;
    private float recoilCur;      // 현재 후퇴량 (0 ~ recoilDistance)
    private float recoilTarget;   // 목표 후퇴량
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

        // 끝점 홀드
        if (recoilHoldTimer > 0f)
        {
            recoilHoldTimer -= dt;
            ApplyRecoilVisual();
            return;
        }

        // 목표가 뒤로면 빠르게 킥, 목표가 0이면 천천히 복귀
        float speed = (recoilTarget > recoilCur) ? recoilKickSpeed : recoilReturnSpeed;
        recoilCur = Mathf.MoveTowards(recoilCur, recoilTarget, speed * dt);

        // 뒤로 끝까지 갔으면 잠깐 멈췄다가 복귀 시작
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
