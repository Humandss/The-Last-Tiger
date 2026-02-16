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

    [Header("HUD")]
    [SerializeField] private DebugHudChannel hudChannel = DebugHudChannel.Enemy;
    [SerializeField] private bool showOnHitOnly = true;
    [SerializeField] private int maxLines = 20;

    private void Awake()
    {
        if (autoCollectOnAwake) CollectModules();
    }

    [ContextMenu("CollectModules")]
    public void CollectModules()
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

    /// <summary>
    /// 모듈이 피해/파괴 이벤트를 낼 때 호출
    /// </summary>
    public void NotifyHitEvent()
    {
        if (!showOnHitOnly) return;
        if (ModuleDebugHUD.Instance == null) return;

        if(hudChannel == DebugHudChannel.Enemy)
        {
            string text = BuildBadListText(maxLines);
            ModuleDebugHUD.Instance.Show(this, text);
        }
       
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
        sb.AppendLine($"<b>{gameObject.name}</b>");
        sb.AppendLine();

        if (bad.Count == 0)
        {
            sb.AppendLine("(none)");
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
}
