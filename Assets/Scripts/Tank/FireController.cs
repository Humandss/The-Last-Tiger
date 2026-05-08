using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class FireController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private TankRecoil tankRecoil;

    [Header("Projectiles")]
    [SerializeField] private BallisticManager APShell;
    [SerializeField] private BallisticManager HEShell;

    [Header("Effects")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject dustSmokePrefab;
    [SerializeField] private GameObject heatHazePrefab;
    [SerializeField] private Transform muzzleFxSocket;
    [SerializeField] private Transform dustSpot;
    [SerializeField] private float muzzleFlashLife = 0.15f;
    [SerializeField] private float heatHazeLife = 0.6f;

    [SerializeField] private Vector3 muzzleFlashLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 dustSmokeLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 heatHazeLocalOffset = Vector3.zero;
    [SerializeField] private bool muzzleFlashFollowMuzzle = true;
    [SerializeField] private bool heatHazeFollowMuzzle = true;

    protected virtual bool IsPlayerShell => false;

    protected void SpawnMuzzleFlash()
    {
        if (!muzzleFlashPrefab) return;

        Transform fxT = muzzleFxSocket ? muzzleFxSocket : muzzle;
        if (!fxT) return;

        GameObject fx = PoolManager.Instance.Spawn(muzzleFlashPrefab, fxT.position, fxT.rotation);
        if (fx == null) return;

        if (muzzleFlashFollowMuzzle)
            fx.transform.SetParent(fxT, worldPositionStays: true);

        StartCoroutine(ReturnAfterDelay(fx, muzzleFlashLife));
    }

    protected void SpawnFireDust()
    {
        if (!dustSmokePrefab || !dustSpot) return;

        Vector3 pos = dustSpot.TransformPoint(dustSmokeLocalOffset);
        Quaternion rot = dustSpot.rotation;

        GameObject fx = PoolManager.Instance.Spawn(dustSmokePrefab, pos, rot);
        if (fx == null) return;
        StartCoroutine(ReturnAfterDelay(fx, 1.5f));
    }

    protected void SpawnHeatHaze()
    {
        if (!heatHazePrefab) return;

        Transform fxT = muzzleFxSocket ? muzzleFxSocket : muzzle;
        if (!fxT) return;

        Vector3 pos = fxT.TransformPoint(heatHazeLocalOffset);
        Quaternion rot = fxT.rotation;

        GameObject fx = PoolManager.Instance.Spawn(heatHazePrefab, pos, rot);
        if (fx == null) return;

        if (heatHazeFollowMuzzle)
            fx.transform.SetParent(fxT, worldPositionStays: true);

        StartCoroutine(ReturnAfterDelay(fx, heatHazeLife));
    }

    private IEnumerator ReturnAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj == null) yield break;
        obj.transform.SetParent(null);
        PoolManager.Instance.Return(obj);
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

        GameObject shellGO = PoolManager.Instance.Spawn(projectile.gameObject, spawnPos, Quaternion.LookRotation(shotDir));
        BallisticManager shell = shellGO != null ? shellGO.GetComponent<BallisticManager>() : null;
        if (shell == null)
        {
            Debug.LogWarning("[FireController] BallisticManager component missing on projectile prefab");
            return;
        }

        shell.isPlayerShell = IsPlayerShell;
        shell.Initialize(spawnPos, shotDir);

        SpawnMuzzleFlash();
        SpawnFireDust();
        SpawnHeatHaze();

        // 차체/포신 반동
        if (tankRecoil != null) tankRecoil.Fire(shotDir);
    }
}
