using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

public class CannonFireController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private BallisticManager projectile;

    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private float muzzleFlashLife = 0.15f;
    [SerializeField] private Vector3 muzzleFlashLocalOffset = Vector3.zero;
    [SerializeField] private bool muzzleFlashFollowMuzzle = true;

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

        SpawnMuzzleFlash();
    }

    private void SpawnMuzzleFlash()
    {
        if (!muzzleFlashPrefab || !muzzle) return;

        // muzzle 기준 위치/회전
        Vector3 pos = muzzle.TransformPoint(muzzleFlashLocalOffset);
        Quaternion rot = muzzle.rotation;

        GameObject fx = Instantiate(muzzleFlashPrefab, pos, rot);

        if (muzzleFlashFollowMuzzle)
            fx.transform.SetParent(muzzle, worldPositionStays: true);

    }
}
