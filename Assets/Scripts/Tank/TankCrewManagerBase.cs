using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class TankCrewManagerBase : MonoBehaviour
{
    [Header("Refs")]
    protected ModuleManager moduleManager;

    [Header("Crew Controllers")]
    [SerializeField] protected TankGunner gunnerController;
    [SerializeField] protected DriverController driverController;
    [SerializeField] protected LoaderController loaderController;

    [Header("Module")]
    protected ModuleDamageController gunnerModule;
    protected ModuleDamageController driverModule;
    protected ModuleDamageController loaderModule;
    protected ModuleDamageController machineGunnerModule;
    protected ModuleDamageController commanderModule;

    protected CrewRole machineGunnerFillingRole = CrewRole.None;
    protected CrewRole commanderFillingRole = CrewRole.None;

    [SerializeField] protected float swapDelay = 5f;

    public void SetSwapDelay(float delay) => swapDelay = delay;
    protected static bool IsDestroyed(ModuleDamageController m)
    => m == null || m.State == ModuleState.Destroyed;
    public bool CanOperate() => IsGunnerAvailable() && IsDriverAvailable();

    protected virtual void Start()
    {
        moduleManager = GetComponent<ModuleManager>();
        CollectCrewModules();
        SubscribeEvents();
    }

    protected virtual void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void CollectCrewModules()
    {
        if (moduleManager == null)
        {
            Debug.LogError("[CrewManager] ModuleManager missing.");
            return;
        }

        gunnerModule = moduleManager.GetCrew(ModuleType.Gunner);
        driverModule = moduleManager.GetCrew(ModuleType.Driver);
        loaderModule = moduleManager.GetCrew(ModuleType.Loader);
        machineGunnerModule = moduleManager.GetCrew(ModuleType.MachineGunner);
        commanderModule = moduleManager.GetCrew(ModuleType.Commander);

        Debug.Log($"[CrewManager] Collected gunner={gunnerModule?.PartName} driver={driverModule?.PartName} loader={loaderModule?.PartName} mg={machineGunnerModule?.PartName} commander={commanderModule?.PartName}");
    }

    private void SubscribeEvents()
    {
        if (gunnerModule != null) gunnerModule.OnStateChanged += OnCrewStateChanged;
        if (driverModule != null) driverModule.OnStateChanged += OnCrewStateChanged;
        if (loaderModule != null) loaderModule.OnStateChanged += OnCrewStateChanged;
        if (machineGunnerModule != null) machineGunnerModule.OnStateChanged += OnCrewStateChanged;
        if (commanderModule != null) commanderModule.OnStateChanged += OnCrewStateChanged;
    }

    private void UnsubscribeEvents()
    {
        if (gunnerModule != null) gunnerModule.OnStateChanged -= OnCrewStateChanged;
        if (driverModule != null) driverModule.OnStateChanged -= OnCrewStateChanged;
        if (loaderModule != null) loaderModule.OnStateChanged -= OnCrewStateChanged;
        if (machineGunnerModule != null) machineGunnerModule.OnStateChanged -= OnCrewStateChanged;
        if (commanderModule != null) commanderModule.OnStateChanged -= OnCrewStateChanged;
    }

    protected ModuleDamageController GetCrewModuleByType(ModuleType type)
    {
        switch (type)
        {
            case ModuleType.Gunner: return gunnerModule;
            case ModuleType.Driver: return driverModule;
            case ModuleType.Loader: return loaderModule;
            case ModuleType.MachineGunner: return machineGunnerModule;
            case ModuleType.Commander: return commanderModule;
            default: return null;
        }
    }

    protected static bool IsCrewModuleType(ModuleType type)
    {
        return type == ModuleType.Commander
            || type == ModuleType.Gunner
            || type == ModuleType.Loader
            || type == ModuleType.Driver
            || type == ModuleType.MachineGunner;
    }

    public virtual void OnCrewStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if(moduleManager.GetAliveCrewCount() < 2) moduleManager.NotifyCrewDepleted();

    }

    public abstract void ApplyInitialStates();
    public abstract bool IsGunnerAvailable();
    public abstract bool IsDriverAvailable();
    public abstract bool IsLoaderAvailable();

    
}
