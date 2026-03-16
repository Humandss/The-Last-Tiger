using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GunDisableBridge : MonoBehaviour
{
    private TankGunner gunner;

    [SerializeField] private ModuleDamageController gunModule;
    [SerializeField] private ModuleDamageController breechModule;

    private void Awake()
    {
        gunner = GetComponent<TankGunner>();
    }

    private void Start()
    {
        if (gunModule != null) gunModule.OnStateChanged += OnGunStateChanged;
        if (breechModule != null) breechModule.OnStateChanged += OnBreechStateChanged;

        ApplyInitialStates();
    }

    private void OnDestroy()
    {
        if (gunModule != null) gunModule.OnStateChanged -= OnGunStateChanged;
        if (breechModule != null) breechModule.OnStateChanged -= OnBreechStateChanged;
    }

    private void OnGunStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (gunner == null) return;
        gunner.SetGunDestroyed(next == ModuleState.Destroyed);
        gunner.SetGunHpRatio(who.Hp01);
        Debug.Log($"[GunBridge] 포신 → state={next} hp={who.Hp01:0.00}");
    }

    private void OnBreechStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (gunner == null) return;
        gunner.SetBreechDestroyed(next == ModuleState.Destroyed);
        Debug.Log($"[GunBridge] 브리치 → state={next}");
    }

    private void ApplyInitialStates()
    {
        if (gunner == null) return;

        if (gunModule != null)
        {
            gunner.SetGunDestroyed(gunModule.State == ModuleState.Destroyed);
            gunner.SetGunHpRatio(gunModule.Hp01);
        }

        if (breechModule != null)
        {
            gunner.SetBreechDestroyed(breechModule.State == ModuleState.Destroyed);
        }
    }
}
