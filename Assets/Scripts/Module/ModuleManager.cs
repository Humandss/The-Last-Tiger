using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ModuleManager : MonoBehaviour
{ 
    [Header("Collect")]
    [SerializeField] private bool autoCollectOnAwake = true;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private List<ModuleDamageController> modules = new();
    private List<ModuleDamageController> aModules = new();
    private List<ModuleDamageController> aCrew = new();

    [Header("HUD")]
    [SerializeField] private DebugHudChannel hudChannel = DebugHudChannel.Enemy;
    [SerializeField] private bool showOnHitOnly = true;
    [SerializeField] private int maxLines = 20;


    private TankFireEffects[] fireEffects;

    public event Action OnTankDestroyed;   
    private bool tankDestroyedFired = false;

    public void NotifyAmmoExplosion() => FireTankDestroyed();

    public void NotifyCrewDepleted() => FireTankDestroyed();

    public void NotifyCommanderDead() => FireTankDestroyed();

    public bool IsOnFire
    {
        get
        {
            for (int i = 0; i < fireEffects.Length; i++)
                if (fireEffects[i] != null && fireEffects[i].IsOnFire) return true;
            return false;
        }
    }

    public void StopAllFires()
    {
        for (int i = 0; i < fireEffects.Length; i++)
            if (fireEffects[i] != null && fireEffects[i].IsOnFire)
                fireEffects[i].StopFire();
    }

    public ModuleDamageController GetCrew(ModuleType type) => GetCrewModule(type);


    private void Awake()
    {
        fireEffects = GetComponentsInChildren<TankFireEffects>(true);
        if (autoCollectOnAwake) CollectModules();

    }

    [ContextMenu("CollectModules")]
    private void CollectModules()
    {
        modules.Clear();
        if (includeInactive) GetComponentsInChildren(true, modules);
        else GetComponentsInChildren(false, modules);

        // 각 모듈이 자기 매니저를 등록
        for (int i = 0; i < modules.Count; i++)
        {
            if (!modules[i]) continue;
            modules[i].BindManager(this);
        }
        Debug.Log($"obj : {gameObject.name}, total module : {modules.Count}");
    }

    public IReadOnlyList<ModuleDamageController> GetAliveInternalModules()
    {
        aModules.Clear();

        for (int i = 0; i < modules.Count; i++)
        {
            var m = modules[i];
            if (!m) continue;
            if (m.State == ModuleState.Destroyed) continue;
            if (m.Side == PartSide.External) continue;

            aModules.Add(m);
        }

        return aModules;
    }

    private IReadOnlyList<ModuleDamageController> GetAliveCrewModules()
    {
        aCrew.Clear();
        for (int i = 0; i < modules.Count; i++)
        {
            var m = modules[i];
            if (!m) continue;
            if (m.State == ModuleState.Destroyed) continue; // 파괴된 거 제외

            // 크루 타입만 포함
            if (m.Type != ModuleType.Driver &&
                m.Type != ModuleType.MachineGunner &&
                m.Type != ModuleType.Gunner &&
                m.Type != ModuleType.Loader &&
                m.Type != ModuleType.Commander) continue;


            aCrew.Add(m);
        }
        return aCrew;
    }
    /// <summary>
    /// 모듈이 피해/파괴 이벤트를 낼 때 호출
    /// </summary>
    public void NotifyHitEvent()
    {
        if (!showOnHitOnly) return;
        if (ModuleDebugHUD.Instance == null) return;

        string text = BuildBadListText(maxLines);
        ModuleDebugHUD.Instance.Show(this, text, hudChannel);
    }

    private string BuildBadListText(int maxLines)
    {
        var bad = new List<ModuleDamageController>();
        for (int i = 0; i < modules.Count; i++)
        {
            var m = modules[i];
            if (!m) continue;

            if (m.State == ModuleState.Damaged || m.State == ModuleState.Destroyed)
                bad.Add(m);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"<size=20><b>{gameObject.name}</b></size>");
        sb.AppendLine();

        if (bad.Count == 0)
        {
            sb.AppendLine("Non-Penetration");
            return sb.ToString();
        }

        // Destroyed 먼저
        bad.Sort((a, b) => ((int)b.State).CompareTo((int)a.State));

        int lines = 0;
        for (int i = 0; i < bad.Count; i++)
        {
            var m = bad[i];
            string col = (m.State == ModuleState.Destroyed) ? "red" : "yellow";
            sb.Append($"<color={col}>");
            sb.Append($">{m.PartName} ({m.State})");
            sb.AppendLine("</color>");

            if (++lines >= maxLines) break;
        }

        return sb.ToString();
    }

    private void FireTankDestroyed()
    {
        if (tankDestroyedFired) return;
        tankDestroyedFired = true;
        int listenerCount = OnTankDestroyed?.GetInvocationList().Length ?? 0;
        Debug.Log($"[ModuleManager] FireTankDestroyed: {gameObject.name}, 구독자={listenerCount}");
        OnTankDestroyed?.Invoke();
    }

    private ModuleDamageController GetCrewModule(ModuleType type)
    {
        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i] && modules[i].Type == type)
                return modules[i];
        }
        return null;
    }

    public int GetAliveCrewCount()
    {
        return GetAliveCrewModules().Count;
    }
}
