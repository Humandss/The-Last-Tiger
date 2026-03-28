using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

public class TankFireEffects : TankEffectsManager
{
    [Header("Refs")]
    [SerializeField] private Transform fireSpawnPoint;

    [Header("Fire Prefab")]
    [SerializeField] private GameObject firePrefab;

    [Header("Options")]
    [SerializeField] private bool onlyOnce = true;
    [SerializeField] private float engineFireChance = 0.3f;

    private GameObject fireInstance;

    public bool IsOnFire => onFire;

    protected override void Awake()
    {
        base.Awake();
        // Engine: damaged에서 30% 발화
        module.OnDamaged += OnDamaged;
        module.OnHit += OnHit;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        module.OnDamaged -= OnDamaged;
        module.OnHit -= OnHit;
    }
    protected override void Update()
    {
        base.Update();
    }

    public override void OnStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        base.OnStateChanged(who, prev, next);
        // FuelTank 파괴 시 불
        if (who.Type == ModuleType.FuelTank && next == ModuleState.Destroyed) SpawnFire();
    }
    private void OnDamaged(ModuleDamageController who, float dmg, DamageType type)
    {
        // 엔진 피격 시 30% 확률 불 (파괴 전에도 가능)
        if (who.Type != ModuleType.Engine) return;

        // 이미 불이면 또 시도하지 않기
        if (onlyOnce && fireInstance != null) return;

        if (Random.value < engineFireChance) SpawnFire();

    }
    private void OnHit(ModuleDamageController who, DamageType type)
    {
        if (onFire) return;
        if (who.Type != ModuleType.FuelTank) return;
        if (who.State != ModuleState.Destroyed) return; //파괴되지 않은 상태면 호출x
        
        SpawnFire();
    }

    public override void StopFire()
    {
        onFire = false;
        life = 0f;
        t = 0f;

        if (fireInstance)
        {
            WreckEffectManager.Instance?.Unregister(fireInstance);
            fireInstance.transform.SetParent(null);
            PoolManager.Instance.Return(fireInstance);
        }
        fireInstance = null;

        soundController?.StopFire();
    }
    public override void TickDamage()
    {
        var list = moduleMgr.GetAliveInternalModules();
        for (int i = 0; i < list.Count; i++)
        {
            list[i].TakeDamage(0.0f, DamageType.DefaultFire);
        }
    }
    public override void SpawnFire()
    {
        if (!firePrefab)
        {
            Debug.LogWarning("[TankFireEffects] firePrefab not set!");
            return;
        }
        onFire = true;
        Transform t = fireSpawnPoint ? fireSpawnPoint : transform;
        Vector3 spawnPos = t.TransformPoint(localOffset);

        fireInstance = PoolManager.Instance.Spawn(firePrefab, spawnPos, Quaternion.identity);

        if (followParent)
            fireInstance.transform.SetParent(t, worldPositionStays: true);

        WreckEffectManager.Instance?.Register(fireInstance, t);

        soundController?.PlayFire();
    }
}
