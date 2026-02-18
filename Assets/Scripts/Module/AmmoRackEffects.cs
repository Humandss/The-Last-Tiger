using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.CullingGroup;

public class AmmoRackEffects : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ModuleDamageController module;
    [SerializeField] private Transform fireSpawnPoint;
    [SerializeField] private Transform explosionPoint;
    [SerializeField] private ModuleManager moduleMgr;
    [SerializeField] private TurretBlowOff blowoff;

    [Header("Prefabs")]
    [SerializeField] private GameObject ammoExplosionPrefab;
    [SerializeField] private GameObject ammoFirePrefab;
    [SerializeField] private GameObject ammoSmokePrefab;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private bool followParent = true;

    [Header("AmmoRack Info")]
    [SerializeField] private float tickInterval = 1.5f;

    [Header("Ammo Rack Failure")]
    [SerializeField, Range(0f, 1f)] private float ammoExplosionChance = 0.75f;

    [Header("Lifetime")]
    [SerializeField] private bool autoStopAfterTime = false;
    [SerializeField] private float stopAfterSeconds = 15f;

    private readonly List<ModuleDamageController> modules = new();
    private float t;
    private float life;

    private GameObject fireInstance;
    private GameObject smokeInstance;
    private GameObject explosionInstance;

    private bool onFire = false;
    private bool ammoEventTriggered = false;

    private void Update()
    {
        if (onFire)
        {
            if (autoStopAfterTime)
            {
                life += Time.deltaTime;
                if (life >= stopAfterSeconds)
                {
                    StopFireAndSpawnSmoke();
                    Destroy(gameObject);
                    return;
                }
            }

            t -= Time.deltaTime;
            if (t > 0f) return;
            t = Mathf.Max(0.05f, tickInterval);

            TickDamage();
        }

    }
    private void Awake()
    {
        if (!module) module = GetComponent<ModuleDamageController>();

        if (module != null)
            module.OnStateChanged += OnStateChanged;
        else
            Debug.LogWarning("[AmmoRackEffects] ModuleDamageController not found!");
    }

    private void OnDestroy()
    {
        if (module != null)
            module.OnStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (next != ModuleState.Destroyed) return;
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

        Invoke(nameof(SpawnSmoke), 1.5f);
        Debug.LogWarning("[AMMO] Explosion!");
    }

    private void SpawnFire()
    {
        if (!ammoFirePrefab)
        {
            Debug.LogWarning("[AmmoRackEffects] firePrefab not set!");
            return;
        }
        onFire = true;
        Transform t = fireSpawnPoint ? fireSpawnPoint : transform;

        fireInstance = Instantiate(ammoFirePrefab);
        fireInstance.transform.position = t.TransformPoint(localOffset);

        if (followParent)
            fireInstance.transform.SetParent(t, worldPositionStays: true);

        Debug.Log($"[FIRE] Ammo destroyed -> fire spawned on {gameObject.name}");
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

        Debug.Log($"[FIRE] AmmoRack Finish -> smoke spawned on {gameObject.name}");
    }
    private void StopFireAndSpawnSmoke()
    {
        onFire = false;

        if (fireInstance) Destroy(fireInstance);

        SpawnSmoke();
    }

    private void TickDamage()
    {
        var list = moduleMgr.GetAliveInternalModules();
        for (int i = 0; i < list.Count; i++)
        {
            list[i].TakeDamage(0.0f, DamageType.AmmoFire);
        }
    }
   
}
