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
    [SerializeField] private GameObject ammoExplosionPrefab;
    [SerializeField] private GameObject ammoFirePrefab;
    [SerializeField] private GameObject ammoSmokePrefab;


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
    [SerializeField] private UnityEvent onTankDestroyed; 
    [SerializeField] private float deathDelay = 0.3f;

    private GameObject fireInstance;
    private GameObject smokeInstance;
    private GameObject explosionInstance;

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

        float r = Random.value;
        if (r < ammoExplosionChance)
        {
            TriggerAmmoExplosion();
        }
        else
        {
            SpawnFire();
        }

        Invoke(nameof(TriggerDeath), deathDelay);
    }
    private void TriggerAmmoExplosion()
    {
        if (!ammoExplosionPrefab)
        {
            Debug.LogWarning("[AmmoRackEffects] ammoExplosion not set!");
            return;
        }

        Transform t = explosionPoint ? explosionPoint : transform;

        explosionInstance = Instantiate(ammoExplosionPrefab);
        explosionInstance.transform.position = t.TransformPoint(localOffset);

        blowoff.BlowOff(explosionPoint.position);

        if (followParent)
            explosionInstance.transform.SetParent(t, worldPositionStays: true);

        var list = moduleMgr.GetAliveInternalModules();
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (!m) continue;

            m.TakeDamage(0.0f, DamageType.AmmoRack);
        }

        Invoke(nameof(SpawnSmoke), 0.5f);
        soundController.PlayAmmoPop();
        soundController.PlayAmmoExplosion();
        Debug.LogWarning("[AMMO] Explosion!");
    }
    private void TriggerDeath()
    {
        Debug.LogWarning($"[AMMO] {gameObject.name} 전차 사망!");
        onTankDestroyed?.Invoke();
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

        smokeInstance = Instantiate(ammoSmokePrefab);
        smokeInstance.transform.position = t.TransformPoint(localOffset);

        if (followParent)
            smokeInstance.transform.SetParent(t, worldPositionStays: true);

        //Debug.Log($"[FIRE] AmmoRack Finish -> smoke spawned on {gameObject.name}");
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
        Destroy(vfx, delay);
    }

    private void StartAmmoPopLoop()
    {
        if (ammoPopRoutine != null) return;
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

            float t = Random.Range(popMinInterval, popMaxInterval);
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

        fireInstance = Instantiate(ammoFirePrefab);
        fireInstance.transform.position = t.TransformPoint(localOffset);

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

    }

    public override void TickDamage()
    {
        var list = moduleMgr.GetAliveInternalModules();

        for (int i = 0; i < list.Count; i++)
        {
            list[i].TakeDamage(0.0f, DamageType.AmmoFire);
        }
    }
}
