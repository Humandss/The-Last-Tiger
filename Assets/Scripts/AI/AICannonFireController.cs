using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AICannonFireController : MonoBehaviour, ICannonFire
{
    [Header("Refs")]
    [SerializeField] private Transform muzzle;

    [Header("Projectiles")]
    [SerializeField] private BallisticManager APShell;
    [SerializeField] private BallisticManager HEShell;

    [Header("Effects")] // 이펙트는 살림
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject dustSmokePrefab;
    [SerializeField] private Transform muzzleFxSocket;
    [SerializeField] private Transform dustSpot;
    [SerializeField] private float muzzleFlashLife = 0.15f;

    [SerializeField] private float muzzleOffset = 0.05f;

    public void FireProjectile(Vector3 dir, AmmoType type)
    {
        if (muzzle == null)
        {
            Debug.LogWarning("[AICannonFire] muzzle is NULL");
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
            Debug.LogWarning($"[AICannonFire] Unknown AmmoType: {type}");
            return;
        }

        Vector3 shotDir = dir.sqrMagnitude > 1e-8f ? dir.normalized : muzzle.forward.normalized;
        Vector3 spawnPos = muzzle.position + shotDir * muzzleOffset;

        var shell = Instantiate(projectile, spawnPos, Quaternion.LookRotation(shotDir));
        if (shell == null)
        {
            Debug.LogWarning("[AICannonFire] Instantiate 실패");
            return;
        }

        shell.Initialize(spawnPos, shotDir);
        SpawnMuzzleFlash();
        SpawnFireDust();
    }

    private void SpawnMuzzleFlash()
    {
        if (!muzzleFlashPrefab) return;
        Transform fxT = muzzleFxSocket ? muzzleFxSocket : muzzle;
        var fx = Instantiate(muzzleFlashPrefab, fxT.position, fxT.rotation);
        fx.transform.SetParent(fxT, worldPositionStays: true);
        Destroy(fx, muzzleFlashLife);
    }

    private void SpawnFireDust()
    {
        if (!dustSmokePrefab || !dustSpot) return;
        var fx = Instantiate(dustSmokePrefab, dustSpot.position, dustSpot.rotation);
        Destroy(fx, 1.5f);
    }
}
