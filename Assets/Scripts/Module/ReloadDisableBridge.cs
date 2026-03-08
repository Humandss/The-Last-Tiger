using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ReloadDisableBridge : MonoBehaviour
{
    private LoaderController loader;

    [SerializeField] private ModuleDamageController loaderModule;

    private void Awake()
    {
        loader = GetComponent<LoaderController>();
    }

    private void Start()
    {
        if (loaderModule != null) loaderModule.OnStateChanged += OnLoaderStateChanged;
        ApplyInitialState();
    }

    private void OnDestroy()
    {
        if (loaderModule != null) loaderModule.OnStateChanged -= OnLoaderStateChanged;
    }

    private void OnLoaderStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (loader == null) return;
        bool dead = next == ModuleState.Destroyed;
        loader.SetLoaderState(dead, who.Hp01);
        Debug.Log($"[ReloadBridge] 로더 → dead={dead} hp={who.Hp01:0.00}");
    }

    private void ApplyInitialState()
    {
        if (loader == null || loaderModule == null) return;
        bool dead = loaderModule.State == ModuleState.Destroyed;
        loader.SetLoaderState(dead, loaderModule.Hp01);
    }
}
