using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class TankEffectsManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] protected ModuleDamageController module;
    [SerializeField] protected ModuleManager moduleMgr;
    protected SoundController soundController;

    [Header("Tick")]
    [SerializeField] protected float tickInterval = 1.5f;

    [Header("Prefabs Options")]
    [SerializeField] protected Vector3 localOffset = Vector3.zero;
    [SerializeField] protected bool followParent = true;

    [Header("Lifetime")]
    [SerializeField] private bool autoStopAfterTime = false;
    [SerializeField] private float stopAfterSeconds = 15f;
    protected float t;
    protected float life;
    [SerializeField] protected bool onFire = false;

    protected readonly List<ModuleDamageController> modules = new();

    protected virtual void Awake()
    {
        if(!soundController) soundController = GetComponentInParent<SoundController>();

        if (!module) module = GetComponent<ModuleDamageController>();

        if (module != null)
            module.OnStateChanged += OnStateChanged;
        else
            Debug.LogWarning("[TankEffectsManager] ModuleDamageController not found!");

        if(onFire) SpawnFire();
    }

    protected virtual void OnDestroy()
    {
        if (!module) return;
        module.OnStateChanged -= OnStateChanged;
    }

    protected virtual void Update()
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
    public virtual void OnStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (next != ModuleState.Destroyed) return;
    }

    public abstract void TickDamage();
    public abstract void StopFire();
    public abstract void SpawnFire();
}
