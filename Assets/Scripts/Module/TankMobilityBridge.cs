using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TankMobilityBridge : MonoBehaviour
{
    private DriverController driver;

    [SerializeField] private ModuleDamageController engineModule;
    [SerializeField] private ModuleDamageController transmissionModule;
    [SerializeField] private ModuleDamageController leftTrackModule;
    [SerializeField] private ModuleDamageController rightTrackModule;

    [SerializeField, Range(0f, 1f)] private float minMul = 0.15f;

    private void Awake()
    {
        driver = GetComponent<DriverController>();
    }

    private void Start()
    {
        if (engineModule != null) engineModule.OnStateChanged += OnMobilityStateChanged;
        if (transmissionModule != null) transmissionModule.OnStateChanged += OnMobilityStateChanged;
        if (leftTrackModule != null) leftTrackModule.OnStateChanged += OnTrackStateChanged;
        if (rightTrackModule != null) rightTrackModule.OnStateChanged += OnTrackStateChanged;

        ApplyInitialStates();
    }

    private void OnDestroy()
    {
        if (engineModule != null) engineModule.OnStateChanged -= OnMobilityStateChanged;
        if (transmissionModule != null) transmissionModule.OnStateChanged -= OnMobilityStateChanged;
        if (leftTrackModule != null) leftTrackModule.OnStateChanged -= OnTrackStateChanged;
        if (rightTrackModule != null) rightTrackModule.OnStateChanged -= OnTrackStateChanged;
    }

    private void OnMobilityStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (driver == null) return;
        ApplyMobilityState();
    }

    private void OnTrackStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (driver == null) return;
        ApplyTrackState();
    }

    private void ApplyMobilityState()
    {
        bool immobilized = IsDestroyed(engineModule) || IsDestroyed(transmissionModule);

        float e = engineModule != null ? engineModule.Hp01 : 1f;
        float t = transmissionModule != null ? transmissionModule.Hp01 : 1f;
        float mul = Mathf.Max(minMul, Mathf.Min(e, t));

        driver.SetMobilityModuleState(
            canMove: !immobilized,
            maxSpeedMul01: immobilized ? 0f : mul
        );

        Debug.Log($"[MobilityBridge] 기동력 → imm={immobilized} mul={mul:0.00}");
    }

    private void ApplyTrackState()
    {
        bool l = IsDestroyed(leftTrackModule);
        bool r = IsDestroyed(rightTrackModule);
        driver.SetTrackState(l, r);
        Debug.Log($"[MobilityBridge] 궤도 → left={l} right={r}");
    }

    private void ApplyInitialStates()
    {
        if (driver == null) return;
        ApplyMobilityState();
        ApplyTrackState();
    }

    private static bool IsDestroyed(ModuleDamageController m)
        => m == null || m.State == ModuleState.Destroyed;
}
