using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

public class CannonFireController : MonoBehaviour
{
    [Header("Refs")]
    
    [SerializeField] private Transform muzzle;
    [SerializeField] private BallisticManager projectile;
    private Vector3 dir;
    private void Initialize()
    {
        if (muzzle == null)
        {
            Debug.LogWarning("[CannonFireController] muzzle is NULL");
        }
        if (projectile == null)
        {
            Debug.LogWarning("[CannonFireController] projectile is NULL");
        }

        dir = muzzle.forward.normalized;

        Vector3 spawnPos = muzzle.position + dir * 0.05f;

        GameObject shellObj = Instantiate(projectile.gameObject, spawnPos, Quaternion.LookRotation(dir));
        var shell = shellObj.GetComponent<BallisticManager>();

        shell.Initialize(spawnPos, dir);
    }

    public void FireProjectile()
    {
        Initialize();
    }
}
