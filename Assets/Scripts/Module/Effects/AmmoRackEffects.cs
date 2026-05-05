using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.CullingGroup;

public class AmmoRackEffects : TankEffectsManager
{
    [Header("Refs")]
    [SerializeField] private Transform fireSpawnPoint;
    [SerializeField] private Transform explosionPoint;
    [SerializeField] private TurretBlowOff blowoff;

    [Header("Prefabs")]
    [SerializeField] private GameObject shockWavePrefab;
    [SerializeField] private GameObject ammoExplosionPrefab;
    [SerializeField] private GameObject ammoFirePrefab;
    [SerializeField] private GameObject ammoSmokePrefab;
    [SerializeField] private GameObject heatHazePrefab;
    [SerializeField] private float heatHazeLife = 1.5f;


    [Header("Ammo Rack Failure")]
    [SerializeField, Range(0f, 1f)] private float ammoExplosionChance = 0.75f;

    [Header("Ammo Pop Loop")]
    [SerializeField] private float popMinInterval = 0.25f;
    [SerializeField] private float popMaxInterval = 1.2f;
    [SerializeField] private float popStartDelay = 0.1f;

    [Header("Lifetime")]
    [SerializeField] private float fadeOutSeconds = 2.5f;
    [SerializeField] private float destroyBuffer = 0.5f;

    [Header("Death")]
    [SerializeField] private float deathDelay = 0.3f;

    private GameObject fireInstance;
    private GameObject smokeInstance;
    private GameObject explosionInstance;
    private GameObject shockWaveInstance;

    private Coroutine ammoPopRoutine;
    private bool ammoEventTriggered = false;

    protected override void Awake()
    {
        base.Awake();

    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
    protected override void Update()
    {
        base.Update();

    }
    public override void OnStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        base.OnStateChanged(who, prev, next);

        if (who.Type != ModuleType.Ammo) return;

        TriggerAmmoRackFailure();

    }
    private void TriggerAmmoRackFailure()
    {
        if (ammoEventTriggered) return;
        ammoEventTriggered = true;

        float r = UnityEngine.Random.value;

        if (r < ammoExplosionChance) TriggerAmmoExplosion();
        else SpawnFire();

        moduleMgr.NotifyAmmoExplosion();

        // 플레이어 탱크일 때만 크루 무전음 + 자막 차단
        var playerSound = moduleMgr.GetComponent<PlayerTankSoundController>();
        if (playerSound != null)
        {
            playerSound.SilenceCrewVoices();
            CrewSubtitleOverlay.Instance?.SilenceAll();
        }
    }
    private void TriggerAmmoExplosion()
    {
        if (!ammoExplosionPrefab)
        {
            Debug.LogWarning("[AmmoRackEffects] ammoExplosion not set!");
            return;
        }

        Transform t = explosionPoint ? explosionPoint : transform;
        Vector3 spawnPos = t.TransformPoint(localOffset);

        shockWaveInstance = PoolManager.Instance.Spawn(shockWavePrefab, spawnPos, Quaternion.identity);
        explosionInstance = PoolManager.Instance.Spawn(ammoExplosionPrefab, spawnPos, Quaternion.identity);

        // 열왜곡 (아지랑이) — 폭발 잔열로 공기 일렁임
        if (heatHazePrefab != null)
        {
            GameObject hazeInstance = PoolManager.Instance.Spawn(heatHazePrefab, spawnPos, Quaternion.identity);
            if (hazeInstance != null)
                PoolManager.Instance.ReturnDelayed(hazeInstance, heatHazeLife);
        }

        blowoff.BlowOff(explosionPoint.position);

        if (followParent)
            explosionInstance.transform.SetParent(t, worldPositionStays: true);

        var list = moduleMgr.GetAliveInternalModules();
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (!m) continue;
            if (m.Type == ModuleType.Ammo) continue;

            m.TakeDamage(0.0f, DamageType.AmmoRack);
        }

        Invoke(nameof(SpawnSmoke), 0.5f);
        soundController.PlayAmmoPop();
        soundController.PlayAmmoExplosion();
        Debug.LogWarning("[AMMO] Explosion!");
    }


    private void SpawnSmoke()
    {
        if (!ammoSmokePrefab)
        {
            Debug.LogWarning("[AmmoRackEffects] ammoSmokePrefab not set!");
            return;
        }
        if (smokeInstance) return;

        Transform t = explosionPoint ? explosionPoint : transform;
        Vector3 spawnPos = t.TransformPoint(localOffset);

        smokeInstance = PoolManager.Instance.Spawn(ammoSmokePrefab, spawnPos, Quaternion.identity);

        if (followParent)
            smokeInstance.transform.SetParent(t, worldPositionStays: true);

        WreckEffectManager.Instance?.Register(smokeInstance, t);
    }


    private void StopVfxSlow(GameObject vfx)
    {
        if (!vfx) return;

        float maxLife = 0f;

        var psList = vfx.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in psList)
        {
            var main = ps.main;
            maxLife = Mathf.Max(maxLife, main.startLifetime.constantMax);

            // 남은 파티클만 자연 소멸
            ps.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
        }

        // 트레일 잔상
        float maxTrail = 0f;
        var trails = vfx.GetComponentsInChildren<TrailRenderer>(true);
        foreach (var tr in trails) maxTrail = Mathf.Max(maxTrail, tr.time);

        float delay = Mathf.Max(fadeOutSeconds, maxLife, maxTrail) + destroyBuffer;
        vfx.transform.SetParent(null);
        PoolManager.Instance.ReturnDelayed(vfx, delay);
    }

    private void StartAmmoPopLoop()
    {
        if (ammoPopRoutine != null && ammoEventTriggered) return;
        ammoPopRoutine = StartCoroutine(CoAmmoPopLoop());
    }

    private void StopAmmoPopLoop()
    {
        if (ammoPopRoutine == null) return;
        StopCoroutine(ammoPopRoutine);
        ammoPopRoutine = null;
    }

    private IEnumerator CoAmmoPopLoop()
    {
        if (popStartDelay > 0f)
            yield return new WaitForSeconds(popStartDelay);

        while (onFire)
        {
            soundController.PlayAmmoPop();

            float t = UnityEngine.Random.Range(popMinInterval, popMaxInterval);
            yield return new WaitForSeconds(t);
        }

        ammoPopRoutine = null;
    }

    public override void SpawnFire()
    {
        if (!ammoFirePrefab)
        {
            Debug.LogWarning("[AmmoRackEffects] firePrefab not set!");
            return;
        }

        onFire = true;
        StartAmmoPopLoop();
        Transform t = fireSpawnPoint ? fireSpawnPoint : transform;
        Vector3 spawnPos = t.TransformPoint(localOffset);

        fireInstance = PoolManager.Instance.Spawn(ammoFirePrefab, spawnPos, Quaternion.identity);

        if (followParent)
            fireInstance.transform.SetParent(t, worldPositionStays: true);

        Invoke(nameof(SpawnSmoke), 2.5f);

        Debug.Log($"[FIRE] Ammo destroyed -> fire spawned on {gameObject.name}");
    }

    public override void StopFire()
    {
        onFire = false;
        StopAmmoPopLoop();

        if (fireInstance)
        {
            StopVfxSlow(fireInstance);
            fireInstance = null;
        }

        if (smokeInstance)
        {
            StopVfxSlow(smokeInstance);
            smokeInstance = null;
        }
    }

    public override void TickDamage()
    {
        var list = moduleMgr.GetAliveInternalModules();

        for (int i = 0; i < list.Count; i++)
        {
            var module = list[i];
            if (!module) continue;
            if (module.Type == ModuleType.Ammo) continue;

            module.TakeDamage(0.0f, DamageType.AmmoFire);
        }
    }
}
