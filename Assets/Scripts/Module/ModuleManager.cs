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

    [Header("Destroyed Debug")]
    [SerializeField] private bool logDestroyed = true;
    [SerializeField] private float logInterval = 0.5f;
    [SerializeField] private bool logOnlyWhenChanged = true;

    [Tooltip("화면에도 띄울지(간단 OnGUI)")]
    [SerializeField] private bool showOnScreen = true;

    private float _t;
    private string _lastSig = "";
    private string _cachedText = "";

    private void Awake()
    {
        if (autoCollectOnAwake) CollectModules();
    }

    [ContextMenu("CollectModules")]
    public void CollectModules()
    {
        modules.Clear();

        if (includeInactive)
            GetComponentsInChildren(true, modules);
        else
            GetComponentsInChildren(false, modules);

        Debug.Log($"[ModuleMgr] Collected {modules.Count} ModuleDamageController(s).");
    }

    private void Update()
    {
        if (!logDestroyed && !showOnScreen) return;

        _t -= Time.deltaTime;
        if (_t > 0f) return;
        _t = Mathf.Max(0.05f, logInterval);

        // Destroyed 목록 만들기
        var destroyed = GetDestroyedModules(out string signature);

        // 바뀐 경우만 로그
        if (logOnlyWhenChanged && signature == _lastSig)
            return;

        _lastSig = signature;

        if (destroyed.Count == 0)
        {
            _cachedText = "Destroyed Modules: (none)";
            if (logDestroyed) Debug.Log("[ModuleMgr] Destroyed Modules: (none)");
            return;
        }

        var sb = new StringBuilder();
        sb.Append("Destroyed Modules (").Append(destroyed.Count).Append("): ");

        for (int i = 0; i < destroyed.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(destroyed[i]);
        }

        _cachedText = sb.ToString();

        if (logDestroyed)
            Debug.Log("[ModuleMgr] " + _cachedText);
    }

    private List<string> GetDestroyedModules(out string signature)
    {
        var list = new List<string>();
        var sig = new StringBuilder();

        for (int i = 0; i < modules.Count; i++)
        {
            var m = modules[i];
            if (!m) continue;

            // ModuleDamageController에 State 프로퍼티가 없으면
            // (hp <= 0f) 같은 조건으로 바꿔줘야 함.
            if (m.State == ModuleState.Destroyed)
            {
                string name = $"{m.GetModuleType()}:{m.gameObject.name}";
                list.Add(name);

                // 변화 감지용 시그니처
                sig.Append(name).Append("|");
            }
        }

        signature = sig.ToString();
        return list;
    }
}
