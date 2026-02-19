using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

public class TankFireEffects : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ModuleDamageController module;
    [SerializeField] private Transform fireSpawnPoint;
    [SerializeField] private ModuleManager moduleMgr;

    [Header("DOT")]
    [SerializeField] private float tickInterval = 1.5f;
    [SerializeField] private float damagePerTick = 2.5f;

    [Header("Fire Prefab")]
    [SerializeField] private GameObject firePrefab;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private bool followParent = true;

    [Header("Options")]
    [SerializeField] private bool onlyOnce = true; // Destroyed 이벤트가 중복 호출돼도 안전
    [SerializeField] private float engineFireChance = 0.3f;

    [Header("Lifetime")]
    [SerializeField] private bool autoStopAfterTime = false;
    [SerializeField] private float stopAfterSeconds = 15f;

    private readonly List<ModuleDamageController> modules = new();
    private float t;
    private float life;

    private GameObject fireInstance;
    private bool onFire = false;
    private void Update()
    {
        if (!onFire) return;

        if (autoStopAfterTime)
        {
            life += Time.deltaTime;
            if (life >= stopAfterSeconds)
            {
                StopFire();
                return;
            }
        }

        t -= Time.deltaTime;
        if (t > 0f) return;
        t = Mathf.Max(0.05f, tickInterval);

        TickDamage();

    }
    private void Reset()
    {
        module = GetComponent<ModuleDamageController>();
        fireSpawnPoint = transform;
    }

    private void Awake()
    {
        if (!module) module = GetComponent<ModuleDamageController>();
        if (!fireSpawnPoint) fireSpawnPoint = transform;

        if (module == null)
        {
            Debug.LogWarning("[TankFireEffects] ModuleDamageController not found!");
            enabled = false;
            return;
        }

        // FuelTank: destroyed에서 발화
        module.OnStateChanged += OnStateChanged;

        // Engine: damaged에서 30% 발화
        module.OnDamaged += OnDamaged;
    }

    private void OnDestroy()
    {
        if (!module) return;
        module.OnStateChanged -= OnStateChanged;
        module.OnDamaged -= OnDamaged;
    }

    private void OnStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (next != ModuleState.Destroyed) return;

        // FuelTank 파괴 시 불
        if (who.Type == ModuleType.FuelTank) SpawnFire();

    }
    private void OnDamaged(ModuleDamageController who, float dmg, DamageType type)
    {
        // 엔진 피격 시 30% 확률 불 (파괴 전에도 가능)
        if (who.Type != ModuleType.Engine) return;

        // 이미 불이면 또 시도하지 않기
        if (onlyOnce && fireInstance != null) return;

        if (Random.value < engineFireChance) SpawnFire();
      
    }
    private void SpawnFire()
    {
        if (!firePrefab)
        {
            Debug.LogWarning("[TankFireEffects] firePrefab not set!");
            return;
        }
        onFire = true;
        Transform t = fireSpawnPoint ? fireSpawnPoint : transform;

        fireInstance = Instantiate(firePrefab);
        fireInstance.transform.position = t.TransformPoint(localOffset);

        if (followParent)
            fireInstance.transform.SetParent(t, worldPositionStays: true);

        Debug.Log($"[FIRE] fire spawned on {gameObject.name}");
    }
    private void StopFire()
    {
        onFire = false;
        life = 0f;
        t = 0f;

        if (fireInstance) Destroy(fireInstance);
        fireInstance = null;

        Debug.Log($"[FIRE] fire stopped on {gameObject.name}");
    }
    private void TickDamage()
    {
        var list = moduleMgr.GetAliveInternalModules();
        for (int i = 0; i < list.Count; i++)
        {
            list[i].TakeDamage(0.0f, DamageType.DefaultFire);
        }
    }
}
