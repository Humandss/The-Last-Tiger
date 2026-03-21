using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCrewManager : TankCrewManagerBase
{

    [Header("AI")]
    [SerializeField] private TankAIController aiController;

    private bool gunnerSwapPending = false;
    private bool driverSwapPending = false;
    private bool loaderSwapPending = false;


    protected override void Start()
    {
        base.Start();
        ApplyInitialStates();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    private void UpdateAICrewSwap()
    {
        bool gunnerDead = IsDestroyed(gunnerModule) || gunnerSwapPending;
        bool driverDead = IsDestroyed(driverModule) || driverSwapPending;
        bool loaderDead = IsDestroyed(loaderModule) || loaderSwapPending;
        bool mgDead = IsDestroyed(machineGunnerModule);
        bool cmdDead = IsDestroyed(commanderModule);

        bool mgAvail = machineGunnerModule != null && !mgDead;
        bool cmdAvail = commanderModule != null && !cmdDead;

        machineGunnerFillingRole = CrewRole.None;
        commanderFillingRole = CrewRole.None;

        bool mgUsed = false;
        bool cmdUsed = false;

        if (gunnerDead)
        {
            if (cmdAvail && !cmdUsed)
            {
                commanderFillingRole = CrewRole.Gunner;
                cmdUsed = true;
                gunnerController?.SetGunnerState(false, commanderModule.Hp01);
            }
            else if (mgAvail && !mgUsed)
            {
                machineGunnerFillingRole = CrewRole.Gunner;
                mgUsed = true;
                gunnerController?.SetGunnerState(false, machineGunnerModule.Hp01);
            }
            else
            {
                gunnerController?.SetGunnerState(true, 0f);
            }
        }
        else
        {
            gunnerController?.SetGunnerState(false, gunnerModule?.Hp01 ?? 1f);
        }

        if (driverDead)
        {
            if (mgAvail && !mgUsed)
            {
                machineGunnerFillingRole = CrewRole.Driver;
                mgUsed = true;
                driverController?.SetDriverState(false, machineGunnerModule.Hp01);
            }
            else if (cmdAvail && !cmdUsed)
            {
                commanderFillingRole = CrewRole.Driver;
                cmdUsed = true;
                driverController?.SetDriverState(false, commanderModule.Hp01);
            }
            else
            {
                driverController?.SetDriverState(true, 0f);
            }
        }
        else
        {
            driverController?.SetDriverState(false, driverModule?.Hp01 ?? 1f);
        }

        if (loaderDead)
        {
            if (mgAvail && !mgUsed)
            {
                machineGunnerFillingRole = CrewRole.Loader;
                mgUsed = true;
                loaderController?.SetLoaderState(false, machineGunnerModule.Hp01);
            }
            else if (cmdAvail && !cmdUsed)
            {
                commanderFillingRole = CrewRole.Loader;
                cmdUsed = true;
                loaderController?.SetLoaderState(false, commanderModule.Hp01);
            }
            else
            {
                loaderController?.SetLoaderState(true, 0f);
            }
        }
        else
        {
            loaderController?.SetLoaderState(false, loaderModule?.Hp01 ?? 1f);
        }

        bool commanderEffectivelyDead = commanderModule == null || cmdDead || commanderFillingRole != CrewRole.None;
        if (aiController != null)
            aiController.SetCommanderDead(commanderEffectivelyDead);
    }

    public override void OnCrewStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        base.OnCrewStateChanged(who, prev, next);

        if (next == ModuleState.Destroyed)
        {
            if (who == gunnerModule && !gunnerSwapPending)
                StartCoroutine(SwapWithDelay(CrewRole.Gunner));
            else if (who == driverModule && !driverSwapPending)
                StartCoroutine(SwapWithDelay(CrewRole.Driver));
            else if (who == loaderModule && !loaderSwapPending)
                StartCoroutine(SwapWithDelay(CrewRole.Loader));
            else
                UpdateAICrewSwap();
        }
        else
        {
            UpdateAICrewSwap();
        }
            
    }


    private IEnumerator SwapWithDelay(CrewRole role)
    {
        SetPending(role, true);
        ApplyRoleDead(role);

        yield return new WaitForSeconds(swapDelay);

        SetPending(role, false);
        UpdateAICrewSwap();
    }

    private void SetPending(CrewRole role, bool pending)
    {
        switch (role)
        {
            case CrewRole.Gunner: gunnerSwapPending = pending; break;
            case CrewRole.Driver: driverSwapPending = pending; break;
            case CrewRole.Loader: loaderSwapPending = pending; break;
        }
    }

    private void ApplyRoleDead(CrewRole role)
    {
        switch (role)
        {
            case CrewRole.Gunner: gunnerController?.SetGunnerState(true, 0f); break;
            case CrewRole.Driver: driverController?.SetDriverState(true, 0f); break;
            case CrewRole.Loader: loaderController?.SetLoaderState(true, 0f); break;
        }
    }

    public override bool IsGunnerAvailable()
    {
        return (!IsDestroyed(gunnerModule) && !gunnerSwapPending)
            || machineGunnerFillingRole == CrewRole.Gunner
            || commanderFillingRole == CrewRole.Gunner;
    }

    public override bool IsDriverAvailable()
    {
        return (!IsDestroyed(driverModule) && !driverSwapPending)
            || machineGunnerFillingRole == CrewRole.Driver
            || commanderFillingRole == CrewRole.Driver;
    }

    public override bool IsLoaderAvailable()
    {
        return (!IsDestroyed(loaderModule) && !loaderSwapPending)
            || machineGunnerFillingRole == CrewRole.Loader
            || commanderFillingRole == CrewRole.Loader;
    }

    public override void ApplyInitialStates()
    {
        UpdateAICrewSwap();
    }


   
 


}
