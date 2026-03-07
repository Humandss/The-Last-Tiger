using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class FireController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform muzzle;

    [Header("Projectiles")]
    [SerializeField] private BallisticManager APShell;
    [SerializeField] private BallisticManager HEShell;

    [Header("Effects")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject dustSmokePrefab;
    [SerializeField] private Transform muzzleFxSocket;
    [SerializeField] private Transform dustSpot;
    [SerializeField] private float muzzleFlashLife = 0.15f;

    [SerializeField] private Vector3 muzzleFlashLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 dustSmokeLocalOffset = Vector3.zero;
    [SerializeField] private bool muzzleFlashFollowMuzzle = true;

    protected void SpawnMuzzleFlash()
    {
        if (!muzzleFlashPrefab) return;

        Transform fxT = muzzleFxSocket ? muzzleFxSocket : muzzle;
        if (!fxT) return;

        GameObject fx = Instantiate(muzzleFlashPrefab, fxT.position, fxT.rotation);

        if (muzzleFlashFollowMuzzle)
            fx.transform.SetParent(fxT, worldPositionStays: true);

        Destroy(fx, muzzleFlashLife);

    }

    protected void SpawnFireDust()
    {
        if (!dustSmokePrefab || !dustSpot) return;

        // muzzle 기준 위치/회전
        Vector3 pos = dustSpot.TransformPoint(dustSmokeLocalOffset);
        Quaternion rot = dustSpot.rotation;

        GameObject fx = Instantiate(dustSmokePrefab, pos, rot);

        Destroy(fx, 1.5f);

    }

    public virtual void FireProjectile(Vector3 dir, AmmoType type)
    {
        if (muzzle == null)
        {
            Debug.LogWarning("[FireController] muzzle is NULL");
            return;
        }

        if (APShell == null && HEShell == null)
        {
            Debug.LogWarning("[FireController] projectile is NULL");
            return;
        }
        BallisticManager projectile = type switch
        {
            AmmoType.AP => APShell,
            AmmoType.HE => HEShell,
            _ => null
        };

        if (projectile == null)
        {
            Debug.LogWarning($"[FireController] Unknown AmmoType: {type}");
            return;
        }

        Vector3 shotDir = dir.sqrMagnitude > 1e-8f ? dir.normalized : muzzle.forward.normalized;
        Vector3 spawnPos = muzzle.position + shotDir * 0.05f;

        var shell = Instantiate(projectile, spawnPos, Quaternion.LookRotation(shotDir));
        if (shell == null)
        {
            Debug.LogWarning("[FireController] BallisticManager component missing on projectile prefab");
            return;
        }

        shell.Initialize(spawnPos, shotDir);

        SpawnMuzzleFlash();
        SpawnFireDust();
    }
}
