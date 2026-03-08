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

    public enum CrewRole { None, Gunner, Driver, Loader }

    private void Start()
    {
        moduleManager = GetComponent<ModuleManager>();
        CollectCrewModules();
        SubscribeEvents();
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
        // 커맨더 사망
        if (who == commanderModule && isAI && aiController != null)
        {
            bool dead = next == ModuleState.Destroyed;
           // aiController.SetCommanderDead(dead);
            Debug.Log($"[CrewManager] 커맨더 → dead={dead}");
        }

        // AI 자동 스와핑
        if (isAI) UpdateAICrewSwap();
    }

    // ===== AI 자동 스와핑 =====

    private void UpdateAICrewSwap()
    {
        bool gunnerDead = IsDestroyed(gunnerModule);
        bool driverDead = IsDestroyed(driverModule);
        bool loaderDead = IsDestroyed(loaderModule);
        bool mgDead = IsDestroyed(machineGunnerModule);
        bool mgAvailable = machineGunnerModule != null && !mgDead;

        CrewRole prevRole = machineGunnerFillingRole;
        machineGunnerFillingRole = CrewRole.None;

        // 1순위: 거너 사망 → 머신거너 대체
        if (gunnerDead && mgAvailable)
        {
            machineGunnerFillingRole = CrewRole.Gunner;
            // 브릿지가 dead=true로 설정한 걸 머신거너 HP로 덮어씀
            gunnerController?.SetGunnerState(false, machineGunnerModule.Hp01);
            Debug.Log("[CrewManager] 머신거너 → 포수 대체");
        }
        // 2순위: 드라이버 사망 → 머신거너 대체
        else if (driverDead && mgAvailable)
        {
            machineGunnerFillingRole = CrewRole.Driver;
            driverController?.SetDriverState(false, machineGunnerModule.Hp01);
            Debug.Log("[CrewManager] 머신거너 → 드라이버 대체");
        }
        // 3순위: 로더 사망 → 머신거너 대체
        else if (loaderDead && mgAvailable)
        {
            machineGunnerFillingRole = CrewRole.Loader;
            loaderController?.SetLoaderState(false, machineGunnerModule.Hp01);
            Debug.Log("[CrewManager] 머신거너 → 로더 대체");
        }

        // 머신거너 사망 → 대체 중이던 역할 다시 사망 처리
        // (브릿지는 원래 모듈 상태만 보므로 여기서 명시적으로 처리)
        if (mgDead && prevRole != CrewRole.None)
        {
            Debug.Log($"[CrewManager] 머신거너 사망 → {prevRole} 대체 해제");
            switch (prevRole)
            {
                case CrewRole.Gunner: gunnerController?.SetGunnerState(true, 0f); break;
                case CrewRole.Driver: driverController?.SetDriverState(true, 0f); break;
                case CrewRole.Loader: loaderController?.SetLoaderState(true, 0f); break;
            }
        }
    }

    // ===== 유틸 =====

    private static bool IsDestroyed(ModuleDamageController m)
        => m == null || m.State == ModuleState.Destroyed;

    // ===== 외부 조회 =====

    public bool IsGunnerAvailable()
        => !IsDestroyed(gunnerModule) || machineGunnerFillingRole == CrewRole.Gunner;

    public bool IsDriverAvailable()
        => !IsDestroyed(driverModule) || machineGunnerFillingRole == CrewRole.Driver;

    public bool IsLoaderAvailable()
        => !IsDestroyed(loaderModule) || machineGunnerFillingRole == CrewRole.Loader;

    public bool IsCommanderAvailable()
        => commanderModule == null || !IsDestroyed(commanderModule);

    /// <summary>
    /// 전차 운용 가능 여부 → Retreat 진입 조건
    /// 커맨더 있는 탱크: 커맨더 + 거너 + 드라이버 생존
    /// 커맨더 없는 탱크: 거너 + 드라이버 생존
    /// </summary>
    public bool CanOperate()
    {
        bool commanderOk = commanderModule == null || IsCommanderAvailable();
        return IsGunnerAvailable() && IsDriverAvailable() && commanderOk;
    }

    /// <summary>
    /// 사격 가능 여부 (Retreat 조건 판별용)
    /// </summary>
    public bool CanFire()
        => IsGunnerAvailable() && IsLoaderAvailable();
}
