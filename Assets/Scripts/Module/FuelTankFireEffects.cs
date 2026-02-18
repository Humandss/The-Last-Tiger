using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class FuelTankFireEffects : MonoBehaviour
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
        if(onFire)
        {
            if(autoStopAfterTime)
        {
                life += Time.deltaTime;
                if (life >= stopAfterSeconds)
                {
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
    private void Reset()
    {
        module = GetComponent<ModuleDamageController>();
        fireSpawnPoint = transform;
    }

    private void Awake()
    {
        if (!module) module = GetComponent<ModuleDamageController>();
        if (!fireSpawnPoint) fireSpawnPoint = transform;

        // Destroyed 상태면(씬 시작부터 이미 파괴) 바로 스폰할지 옵션 원하면 여기서 처리 가능
        // if (module && module.State == ModuleState.Destroyed) SpawnFire();

        if (module != null)
            module.OnStateChanged += OnStateChanged;
        else
            Debug.LogWarning("[FuelTankFireEffects] ModuleDamageController not found!");
    }

    private void OnDestroy()
    {
        if (module != null)
            module.OnStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (next != ModuleState.Destroyed) return;
        if (who.Type != ModuleType.FuelTank) return;


        // 이미 불이 있으면 또 만들지 않기
        if (onlyOnce && fireInstance != null) return;

        SpawnFire();
    }

    private void SpawnFire()
    {
        if (!firePrefab)
        {
            Debug.LogWarning("[FuelTankFireEffects] firePrefab not set!");
            return;
        }
        onFire = true;
        Transform t = fireSpawnPoint ? fireSpawnPoint : transform;

        fireInstance = Instantiate(firePrefab);
        fireInstance.transform.position = t.TransformPoint(localOffset);

        if (followParent)
            fireInstance.transform.SetParent(t, worldPositionStays: true);

        Debug.Log($"[FIRE] Fuel tank destroyed -> fire spawned on {gameObject.name}");
    }

    private void TickDamage()
    {
        var list = moduleMgr.GetAliveInternalModules();
        for (int i = 0; i < list.Count; i++)
        {
            list[i].TakeDamage(0.0f, DamageType.FuelTankFire);
        }
    }
}
