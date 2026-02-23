using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

public class CannonFireController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform muzzleFxSocket;
    [SerializeField] private Transform dustSpot;
    [SerializeField] private BallisticManager projectile;
    [SerializeField] private ShotRecoilShake cameraShotShake;
    [SerializeField] private ShotRecoilShake turretShotShake;

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

    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject dustSmokePrefab;
    [SerializeField] private float muzzleFlashLife = 0.15f;
    [SerializeField] private Vector3 muzzleFlashLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 dustSmokeLocalOffset = Vector3.zero;
    [SerializeField] private bool muzzleFlashFollowMuzzle = true;
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

    public void FireProjectile(Vector3 dir)
    {
        if (muzzle == null)
        {
            Debug.LogWarning("[CannonFireController] muzzle is NULL");
            return;
        }

        if (projectile == null)
        {
            Debug.LogWarning("[CannonFireController] projectile is NULL");
            return;
        }

        Vector3 shotDir = dir.sqrMagnitude > 1e-8f ? dir.normalized : muzzle.forward.normalized;
        Vector3 spawnPos = muzzle.position + shotDir * 0.05f;

        GameObject shellObj = Instantiate(projectile.gameObject, spawnPos, Quaternion.LookRotation(shotDir));
        var shell = shellObj.GetComponent<BallisticManager>();

        if (shell == null)
        {
            Debug.LogWarning("[CannonFireController] BallisticManager component missing on projectile prefab");
            Destroy(shellObj);
            return;
        }

        shell.Initialize(spawnPos, shotDir);


        TriggerShotShake();
        SpawnMuzzleFlash();
        SpawnFireDust();
        TriggerGunRecoil();
       
    }

    private void SpawnMuzzleFlash()
    {
        if (!muzzleFlashPrefab) return;

        Transform fxT = muzzleFxSocket ? muzzleFxSocket : muzzle;
        if (!fxT) return;

        GameObject fx = Instantiate(muzzleFlashPrefab, fxT.position, fxT.rotation);

        if (muzzleFlashFollowMuzzle)
            fx.transform.SetParent(fxT, worldPositionStays: true);

        Destroy(fx, muzzleFlashLife);

    }

    private void SpawnFireDust()
    {
        if (!dustSmokePrefab || !dustSpot) return;

        // muzzle 기준 위치/회전
        Vector3 pos = dustSpot.TransformPoint(dustSmokeLocalOffset);
        Quaternion rot = dustSpot.rotation;

        GameObject fx = Instantiate(dustSmokePrefab, pos, rot);

        Destroy(fx, 1.5f);

    }
    private void TriggerGunRecoil()
    {
        if (recoilPart == null) return;

        // 연사 중에도 반응 좋게: 현재값보다 더 큰 목표로
        recoilTarget = Mathf.Max(recoilTarget, recoilDistance);

    }
    private void ApplyRecoilVisual()
    {
        Vector3 axis = recoilLocalAxis.sqrMagnitude > 1e-6f ? recoilLocalAxis.normalized : Vector3.back;
        recoilPart.localPosition = recoilBaseLocalPos + axis * recoilCur;
    }

    private void TriggerShotShake()
    {
        if (cameraShotShake) cameraShotShake.TriggerKick(cameraShakeIntensity);
        if (turretShotShake) turretShotShake.TriggerKick(turretShakeIntensity);
    }
}
