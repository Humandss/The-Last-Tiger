using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GunDisableBridge : MonoBehaviour
{

    private GunnerController gunner;
    [SerializeField] private bool player;
    [SerializeField] private ModuleDamageController gunnerM;
    [SerializeField] private ModuleDamageController gun;        // 포신 모듈
    [SerializeField] private ModuleDamageController breech;     // 약실/폐쇄기 모듈

    private void Awake()
    {
        if (player) gunner = GetComponent<GunnerController>();

    }

    void Update()
    {
        if (!gunner) return;

        // ===== 거너 =====
        float g = gunnerM ? gunnerM.Hp01 : 1f;
        bool gunnerDead = gunnerM && gunnerM.State == ModuleState.Destroyed;

        if (player)
        {
            gunner.SetGunDestroyed(gun && gun.State == ModuleState.Destroyed);
            gunner.SetBreechDestroyed(breech && breech.State == ModuleState.Destroyed);
            gunner.SetGunHpRatio(gun.Hp01);
            gunner.SetGunnerState(gunnerDead, g);
        }

    }
}
