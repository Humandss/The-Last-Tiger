using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ReloadDisableBridge : MonoBehaviour
{
    private LoaderController loader;
    [SerializeField] private bool player;
    [SerializeField] private ModuleDamageController loaderM;

    private void Awake()
    {
        loader = GetComponent<LoaderController>();
    }

    private void Update()
    {
        if(!loader) return;

        // ===== 로더 =====
        bool loaderDead = loaderM && loaderM.State == ModuleState.Destroyed;
        float l = loaderM ? loaderM.Hp01 : 1f;

        if(player) loader.SetLoaderState(loaderDead, l);
    }
}
