using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 크루 HP 종합 관리 + AI 자동 스와핑
/// </summary>
public class TankCrewManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private bool isAI = false;
    private ModuleManager moduleManager;

    [Header("Crew Controllers")]
    [SerializeField] private TankGunner gunnerController;
    [SerializeField] private DriverController driverController;
    [SerializeField] private LoaderController loaderController;

    [Header("AI")]
    [SerializeField] private TankAIController aiController;

    // 크루 모듈 (ModuleManager에서 가져옴)
    private ModuleDamageController gunnerModule;
    private ModuleDamageController driverModule;
    private ModuleDamageController loaderModule;
    private ModuleDamageController machineGunnerModule;
    private ModuleDamageController commanderModule;

    // 현재 머신거너가 대체 중인 역할
    private CrewRole machineGunnerFillingRole = CrewRole.None;
    private CrewRole commanderFillingRole = CrewRole.None;

    // 교체 딜레이
    private float swapDelay = 0f;

    // 교체 대기 중인 자리 (딜레이 중엔 공석)
    private bool gunnerSwapPending = false;
    private bool driverSwapPending = false;
    private bool loaderSwapPending = false;

    public void SetSwapDelay(float delay) => swapDelay = delay;

    private void Start()
    {
        moduleManager = GetComponent<ModuleManager>();
        CollectCrewModules();
        SubscribeEvents();
        ApplyInitialStates();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    // ===== 모듈 수집 =====

    private void CollectCrewModules()
    {
        if (moduleManager == null)
        {
            Debug.LogError("[CrewManager] ModuleManager가 없습니다!");
            return;
        }

        gunnerModule = moduleManager.GetCrew(ModuleType.Gunner);
        driverModule = moduleManager.GetCrew(ModuleType.Driver);
        loaderModule = moduleManager.GetCrew(ModuleType.Loader);
        machineGunnerModule = moduleManager.GetCrew(ModuleType.MachineGunner);
        commanderModule = moduleManager.GetCrew(ModuleType.Commander);

        Debug.Log($"[CrewManager] 수집 완료 " +
                  $"gunner={gunnerModule?.PartName} " +
                  $"driver={driverModule?.PartName} " +
                  $"loader={loaderModule?.PartName} " +
                  $"mg={machineGunnerModule?.PartName} " +
                  $"commander={commanderModule?.PartName}");
    }

    // ===== 이벤트 구독 =====

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

    // ===== 상태 변화 감지 =====

    private void OnCrewStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (isAI)
        {
            // 사망한 자리에 딜레이 교체 시작
            if (next == ModuleState.Destroyed)
            {
                if (who == gunnerModule && !gunnerSwapPending)
                    StartCoroutine(SwapWithDelay(CrewRole.Gunner));
                else if (who == driverModule && !driverSwapPending)
                    StartCoroutine(SwapWithDelay(CrewRole.Driver));
                else if (who == loaderModule && !loaderSwapPending)
                    StartCoroutine(SwapWithDelay(CrewRole.Loader));
                else
                    // 머신거너/커맨더 사망 시 즉시 재계산
                    UpdateAICrewSwap();
            }
            else
            {
                // HP 변화는 즉시 반영
                UpdateAICrewSwap();
            }
        }
        else
        {
            ApplyCrewState(who, next);
        }
    }
    // ===== 플레이어 크루 상태 전달 =====

    private void ApplyCrewState(ModuleDamageController who, ModuleState next)
    {
        bool dead = next == ModuleState.Destroyed;
        float hp = who.Hp01;

        if (who == gunnerModule)
        {
            gunnerController?.SetGunnerState(dead, hp);
            Debug.Log($"[CrewManager] 거너 → dead={dead} hp={hp:0.00}");
        }
        else if (who == driverModule)
        {
            driverController?.SetDriverState(dead, hp);
            Debug.Log($"[CrewManager] 드라이버 → dead={dead} hp={hp:0.00}");
        }
        else if (who == loaderModule)
        {
            loaderController?.SetLoaderState(dead, hp);
            Debug.Log($"[CrewManager] 로더 → dead={dead} hp={hp:0.00}");
        }
    }

    // ===== AI 자동 스와핑 =====

    private void UpdateAICrewSwap()
    {
        bool gunnerDead = IsDestroyed(gunnerModule) || gunnerSwapPending;
        bool driverDead = IsDestroyed(driverModule) || driverSwapPending;
        bool loaderDead = IsDestroyed(loaderModule) || loaderSwapPending;
        bool mgDead = IsDestroyed(machineGunnerModule);
        bool cmdDead = IsDestroyed(commanderModule);

        bool mgAvail = machineGunnerModule != null && !mgDead;
        bool cmdAvail = commanderModule != null && !cmdDead;

        CrewRole prevMgRole = machineGunnerFillingRole;
        CrewRole prevCmdRole = commanderFillingRole;
        machineGunnerFillingRole = CrewRole.None;
        commanderFillingRole = CrewRole.None;

        // 남은 대체 가능 크루 추적
        bool mgUsed = false;
        bool cmdUsed = false;

        // ===== 거너 자리: 커맨더 우선 → 머신거너 =====
        if (gunnerDead)
        {
            if (cmdAvail && !cmdUsed)
            {
                commanderFillingRole = CrewRole.Gunner;
                cmdUsed = true;
                gunnerController?.SetGunnerState(false, commanderModule.Hp01);
                Debug.Log("[CrewManager] 커맨더 → 거너 대체");
            }
            else if (mgAvail && !mgUsed)
            {
                machineGunnerFillingRole = CrewRole.Gunner;
                mgUsed = true;
                gunnerController?.SetGunnerState(false, machineGunnerModule.Hp01);
                Debug.Log("[CrewManager] 머신거너 → 거너 대체");
            }
            else
            {
                // 대체 불가 → 사망 상태 유지
                gunnerController?.SetGunnerState(true, 0f);
            }
        }
        else
        {
            gunnerController?.SetGunnerState(false, gunnerModule?.Hp01 ?? 1f);
        }

        // ===== 드라이버 자리: 머신거너 우선 → 커맨더 =====
        if (driverDead)
        {
            if (mgAvail && !mgUsed)
            {
                machineGunnerFillingRole = CrewRole.Driver;
                mgUsed = true;
                driverController?.SetDriverState(false, machineGunnerModule.Hp01);
                Debug.Log("[CrewManager] 머신거너 → 드라이버 대체");
            }
            else if (cmdAvail && !cmdUsed)
            {
                commanderFillingRole = CrewRole.Driver;
                cmdUsed = true;
                driverController?.SetDriverState(false, commanderModule.Hp01);
                Debug.Log("[CrewManager] 커맨더 → 드라이버 대체");
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

        // ===== 로더 자리: 머신거너 우선 → 커맨더 =====
        if (loaderDead)
        {
            if (mgAvail && !mgUsed)
            {
                machineGunnerFillingRole = CrewRole.Loader;
                mgUsed = true;
                loaderController?.SetLoaderState(false, machineGunnerModule.Hp01);
                Debug.Log("[CrewManager] 머신거너 → 로더 대체");
            }
            else if (cmdAvail && !cmdUsed)
            {
                commanderFillingRole = CrewRole.Loader;
                cmdUsed = true;
                loaderController?.SetLoaderState(false, commanderModule.Hp01);
                Debug.Log("[CrewManager] 커맨더 → 로더 대체");
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

        // ===== 커맨더 사망 → AI 시야 패널티 =====
        // 커맨더가 다른 역할 대체 중이면 패널티 없음
        bool commanderEffectivelyDead = commanderModule != null &&
                (cmdDead || commanderFillingRole != CrewRole.None);

        if (isAI && aiController != null)
            aiController.SetCommanderDead(commanderEffectivelyDead);

        Debug.Log($"[CrewManager] 스와핑 결과 mg={machineGunnerFillingRole} cmd={commanderFillingRole}");
    }
    // ===== 교체 딜레이 코루틴 =====

    private IEnumerator SwapWithDelay(CrewRole role)
    {
        // 딜레이 중 해당 자리 공석으로 표시
        SetPending(role, true);
        ApplyRoleDead(role);

        Debug.Log($"[CrewManager] {role} 자리 교체 대기 중... ({swapDelay}초)");
        yield return new WaitForSeconds(swapDelay);

        SetPending(role, false);
        UpdateAICrewSwap();
        Debug.Log($"[CrewManager] {role} 자리 교체 완료");
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
    private void ApplyInitialStates()
    {
        if (isAI)
        {
            UpdateAICrewSwap();
        }
        else
        {
            if (gunnerModule != null)
                gunnerController?.SetGunnerState(IsDestroyed(gunnerModule), gunnerModule.Hp01);
            if (driverModule != null)
                driverController?.SetDriverState(IsDestroyed(driverModule), driverModule.Hp01);
            if (loaderModule != null)
                loaderController?.SetLoaderState(IsDestroyed(loaderModule), loaderModule.Hp01);
        }
    }

    // ===== 유틸 =====

    private static bool IsDestroyed(ModuleDamageController m)
        => m == null || m.State == ModuleState.Destroyed;

    // ===== 외부 조회 =====

    public bool IsGunnerAvailable()
         => (!IsDestroyed(gunnerModule) && !gunnerSwapPending)
         || machineGunnerFillingRole == CrewRole.Gunner
         || commanderFillingRole == CrewRole.Gunner;

    public bool IsDriverAvailable()
        => (!IsDestroyed(driverModule) && !driverSwapPending)
        || machineGunnerFillingRole == CrewRole.Driver
        || commanderFillingRole == CrewRole.Driver;

    public bool IsLoaderAvailable()
        => (!IsDestroyed(loaderModule) && !loaderSwapPending)
        || machineGunnerFillingRole == CrewRole.Loader
        || commanderFillingRole == CrewRole.Loader;

    public bool CanOperate()
       => IsGunnerAvailable() && IsDriverAvailable();

}
