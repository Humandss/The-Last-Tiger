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

    [Header("HUD")]
    [SerializeField] private DebugHudChannel hudChannel = DebugHudChannel.Enemy;
    [SerializeField] private bool showOnHitOnly = true;
    [SerializeField] private int maxLines = 20;


    public ModuleDamageController GetCrew(ModuleType type) => GetCrewModule(type);

    private void Awake()
    {
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
            if (modules[i]) modules[i].BindManager(this);
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

    private ModuleDamageController GetCrewModule(ModuleType type)
    {
        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i] && modules[i].Type == type)
                return modules[i];
        }
        return null;
    }
}
