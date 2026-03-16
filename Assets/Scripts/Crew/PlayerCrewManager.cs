using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCrewManager : TankCrewManagerBase
{

    private readonly Dictionary<ModuleType, ModuleType> playerAssignments = new Dictionary<ModuleType, ModuleType>();
    private Coroutine playerSwapRoutine;
    private Coroutine playerRepairRoutine;

    private float playerSwapEndsAt = -1f;
    private ModuleType repairingCrewType = ModuleType.Commander;
    private ModuleDamageController repairingModule;
    private float playerRepairEndsAt = -1f;

    private ModuleType movingCrewType = ModuleType.Commander;
    private ModuleType movingTargetSeat = ModuleType.Commander;

    public bool IsPlayerSwapInProgress => movingCrewType != ModuleType.Commander;
    public float PlayerSwapSecondsRemaining => Mathf.Max(0f, playerSwapEndsAt - Time.time);
    public bool IsPlayerRepairInProgress => repairingCrewType != ModuleType.Commander;
    public float PlayerRepairSecondsRemaining => Mathf.Max(0f, playerRepairEndsAt - Time.time);
    public ModuleType RepairingCrewType => repairingCrewType;
    public ModuleDamageController RepairingModule => repairingModule;
    public ModuleType MovingCrewType => movingCrewType;
    public ModuleType MovingTargetSeat => movingTargetSeat;

    protected override void Start()
    {
        base.Start();
        InitializePlayerAssignments();
        ApplyInitialStates();
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    private void InitializePlayerAssignments()
    {

        playerAssignments.Clear();

        if (!IsDestroyed(driverModule))
            playerAssignments[ModuleType.Driver] = ModuleType.Driver;
        if (!IsDestroyed(gunnerModule))
            playerAssignments[ModuleType.Gunner] = ModuleType.Gunner;
        if (!IsDestroyed(loaderModule))
            playerAssignments[ModuleType.Loader] = ModuleType.Loader;
    }

    private void ApplyPlayerAssignments()
    {

        ModuleDamageController gunnerOccupant = GetOccupantForSeat(ModuleType.Gunner);
        ModuleDamageController driverOccupant = GetOccupantForSeat(ModuleType.Driver);
        ModuleDamageController loaderOccupant = GetOccupantForSeat(ModuleType.Loader);

        bool gunnerBlocked = IsCrewUnavailableForPlayer(ModuleType.Gunner);
        bool driverBlocked = IsCrewUnavailableForPlayer(ModuleType.Driver);
        bool loaderBlocked = IsCrewUnavailableForPlayer(ModuleType.Loader);

        gunnerController?.SetGunnerState(gunnerOccupant == null, gunnerOccupant != null ? gunnerOccupant.Hp01 : 0f);
        driverController?.SetDriverState(driverOccupant == null, driverOccupant != null ? driverOccupant.Hp01 : 0f);
        // An empty loader seat should slow reloads rather than disabling them outright.
        loaderController?.SetLoaderState(false, loaderOccupant != null ? loaderOccupant.Hp01 : 0f);
        gunnerController?.SetGunnerTaskBlocked(gunnerBlocked);
        driverController?.SetDriverTaskBlocked(driverBlocked);
        loaderController?.SetLoaderTaskBlocked(loaderBlocked);
    }

    private void HandlePlayerCrewStateChanged(ModuleDamageController who, ModuleState next)
    {
        if (who == null) return;

        if (next == ModuleState.Destroyed)
        {
            RemoveAssignmentsForCrew(who.Type);
            CancelPlayerActionsForCrew(who.Type);
        }

        ApplyPlayerAssignments();
    }
    private void CancelPlayerActionsForCrew(ModuleType crewType)
    {
        if (playerSwapRoutine != null && movingCrewType == crewType)
            ResetPlayerSwapState(true);

        if (playerRepairRoutine != null && repairingCrewType == crewType)
            ResetPlayerRepairState(true);
    }
    private void ResetPlayerSwapState(bool stopCoroutine)
    {
        if (stopCoroutine && playerSwapRoutine != null)
            StopCoroutine(playerSwapRoutine);

        playerSwapRoutine = null;
        movingCrewType = ModuleType.Commander;
        movingTargetSeat = ModuleType.Commander;
        playerSwapEndsAt = -1f;
    }

    private void ResetPlayerRepairState(bool stopCoroutine)
    {
        if (stopCoroutine && playerRepairRoutine != null)
            StopCoroutine(playerRepairRoutine);

        playerRepairRoutine = null;
        repairingCrewType = ModuleType.Commander;
        repairingModule = null;
        playerRepairEndsAt = -1f;
    }
    public bool CanPlayerMoveCrew(ModuleType sourceCrew, ModuleType targetSeat)
    {
        if (!IsMovableCrew(sourceCrew)) return false;
        if (!IsOperableSeat(targetSeat)) return false;
        if (sourceCrew == ModuleType.Commander) return false;
        if (playerSwapRoutine != null) return false;
        if (IsCrewUnavailableForPlayer(sourceCrew)) return false;

        ModuleDamageController sourceModule = GetCrewModuleByType(sourceCrew);
        if (IsDestroyed(sourceModule)) return false;

        ModuleType currentSeat = GetAssignedSeatForCrew(sourceCrew);
        if (currentSeat == targetSeat) return false;
        if (GetAssignedCrewForSeat(targetSeat).HasValue) return false;

        return true;
    }

    public ModuleType[] GetAvailablePlayerSwapTargets(ModuleType sourceCrew)
    {
        List<ModuleType> targets = new List<ModuleType>();
        ModuleType[] seats = { ModuleType.Driver, ModuleType.Gunner, ModuleType.Loader };

        for (int i = 0; i < seats.Length; i++)
        {
            if (CanPlayerMoveCrew(sourceCrew, seats[i]))
                targets.Add(seats[i]);
        }

        return targets.ToArray();
    }

    public bool StartPlayerCrewMove(ModuleType sourceCrew, ModuleType targetSeat)
    {
        if (!CanPlayerMoveCrew(sourceCrew, targetSeat))
            return false;

        if (playerSwapRoutine != null)
            ResetPlayerSwapState(true);

        playerSwapRoutine = StartCoroutine(CoPlayerCrewMove(sourceCrew, targetSeat));
        return true;
    }

    public ModuleType GetAssignedSeatForCrew(ModuleType crewType)
    {
        if (playerAssignments.TryGetValue(crewType, out ModuleType seat))
            return seat;
        return ModuleType.Commander;
    }

    public ModuleType? GetAssignedCrewForSeat(ModuleType seatType)
    {
        foreach (var pair in playerAssignments)
        {
            if (pair.Value == seatType)
                return pair.Key;
        }

        return null;
    }

    public string GetCrewSeatLabel(ModuleType crewType)
    {
        if (crewType == ModuleType.Commander)
            return "Player";

        if (playerRepairRoutine != null && repairingCrewType == crewType)
            return "Repairing";

        if (playerSwapRoutine != null && movingCrewType == crewType)
            return $"Moving -> {GetSeatDisplayName(movingTargetSeat)}";

        if (!playerAssignments.TryGetValue(crewType, out ModuleType seat))
            return "Unassigned";

        return GetSeatDisplayName(seat);
    }

    public bool CanAssignRepairCrew(ModuleType crewType, ModuleDamageController targetModule)
    {
        if (playerRepairRoutine != null) return false;
        if (!IsMovableCrew(crewType)) return false;
        if (crewType == ModuleType.Commander) return false;
        if (targetModule == null) return false;
        if (IsCrewModuleType(targetModule.Type)) return false;
        if (targetModule.State != ModuleState.Damaged && targetModule.State != ModuleState.Destroyed) return false;
        if (IsCrewUnavailableForPlayer(crewType)) return false;

        ModuleDamageController crewModule = GetCrewModuleByType(crewType);
        if (IsDestroyed(crewModule)) return false;

        return true;
    }

    public bool StartPlayerRepair(ModuleType crewType, ModuleDamageController targetModule)
    {
        if (!CanAssignRepairCrew(crewType, targetModule))
            return false;

        if (playerRepairRoutine != null)
            ResetPlayerRepairState(true);

        playerRepairRoutine = StartCoroutine(CoPlayerRepair(crewType, targetModule));
        return true;
    }

    public float GetRepairDuration(ModuleDamageController targetModule)
    {
        if (targetModule == null)
            return 0f;

        if (targetModule.State == ModuleState.Destroyed)
            return 15f;

        if (targetModule.State == ModuleState.Damaged)
            return Mathf.Clamp((1f - targetModule.Hp01) * 10f, 1f, 10f);

        return 0f;
    }

    private IEnumerator CoPlayerCrewMove(ModuleType sourceCrew, ModuleType targetSeat)
    {
        movingCrewType = sourceCrew;
        movingTargetSeat = targetSeat;
        playerSwapEndsAt = Time.time + Mathf.Max(0.1f, swapDelay);

        playerAssignments.Remove(sourceCrew);
        ApplyPlayerAssignments();

        yield return new WaitForSeconds(Mathf.Max(0.1f, swapDelay));

        ModuleDamageController sourceModule = GetCrewModuleByType(sourceCrew);
        if (!IsDestroyed(sourceModule))
            playerAssignments[sourceCrew] = targetSeat;

        ResetPlayerSwapState(false);

        ApplyPlayerAssignments();
    }

    private IEnumerator CoPlayerRepair(ModuleType crewType, ModuleDamageController targetModule)
    {
        repairingCrewType = crewType;
        repairingModule = targetModule;
        float duration = Mathf.Max(0.1f, GetRepairDuration(targetModule));
        playerRepairEndsAt = Time.time + duration;

        ApplyPlayerAssignments();

        yield return new WaitForSeconds(duration);

        ModuleDamageController crewModule = GetCrewModuleByType(crewType);
        if (!IsDestroyed(crewModule) && targetModule != null)
        {
            if (targetModule.State == ModuleState.Destroyed)
                targetModule.RepairToDamagedState();
            else if (targetModule.State == ModuleState.Damaged)
                targetModule.RepairToFull();
        }

        ResetPlayerRepairState(false);

        ApplyPlayerAssignments();
    }
    private ModuleDamageController GetOccupantForSeat(ModuleType seatType)
    {
        ModuleType? crewType = GetAssignedCrewForSeat(seatType);
        if (!crewType.HasValue) return null;

        if (IsCrewUnavailableForPlayer(crewType.Value))
            return null;

        ModuleDamageController crewModule = GetCrewModuleByType(crewType.Value);
        return IsDestroyed(crewModule) ? null : crewModule;
    }

    private void RemoveAssignmentsForCrew(ModuleType crewType)
    {
        if (playerAssignments.ContainsKey(crewType))
            playerAssignments.Remove(crewType);
    }

    private bool IsCrewUnavailableForPlayer(ModuleType crewType)
    {
        if (repairingCrewType == crewType)
            return true;

        if (movingCrewType == crewType)
            return true;

        return false;
    }

    private static bool IsOperableSeat(ModuleType type)
    {
        return type == ModuleType.Driver || type == ModuleType.Gunner || type == ModuleType.Loader;
    }

    private static bool IsMovableCrew(ModuleType type)
    {
        return type == ModuleType.Driver || type == ModuleType.Gunner || type == ModuleType.Loader || type == ModuleType.MachineGunner;
    }

    private static string GetSeatDisplayName(ModuleType seatType)
    {
        switch (seatType)
        {
            case ModuleType.Driver: return "Driver";
            case ModuleType.Gunner: return "Gunner";
            case ModuleType.Loader: return "Loader";
            default: return seatType.ToString();
        }
    }
    public override void OnCrewStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        HandlePlayerCrewStateChanged(who, next);
    }
    public override bool IsLoaderAvailable()
    {
        return GetOccupantForSeat(ModuleType.Loader) != null;
    }

    public override bool IsDriverAvailable()
    {
        return GetOccupantForSeat(ModuleType.Driver) != null;
    }
    public override bool IsGunnerAvailable()
    {
        return GetOccupantForSeat(ModuleType.Gunner) != null;
    }
    public override void ApplyInitialStates()
    {
        ApplyPlayerAssignments();
    }

}
