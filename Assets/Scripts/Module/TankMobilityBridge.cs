using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TankMobilityBridge : MonoBehaviour
{

    private DriverController driver;
    [SerializeField] private bool player;
    [SerializeField] private ModuleDamageController driverM;
    [SerializeField] private ModuleDamageController engine;        
    [SerializeField] private ModuleDamageController transmission;
    [SerializeField] private ModuleDamageController leftTrack;
    [SerializeField] private ModuleDamageController rightTrack;

    [SerializeField, Range(0f, 1f)] private float minMul = 0.15f; //

    private void Awake()
    {
        driver = GetComponent<DriverController>();
      
    }
    private void Update()
    {
        if (!driver) return;

        // ===== 엔진/트래스미션 =====
        bool imm = (engine && engine.State == ModuleState.Destroyed) || (transmission && transmission.State == ModuleState.Destroyed);

        float e = engine ? engine.Hp01 : 1f;
        float t = transmission ? transmission.Hp01 : 1f;

        float mul = Mathf.Min(e, t);
        mul = Mathf.Max(minMul, mul);
        // ===== 궤도 =====
        bool l = leftTrack && leftTrack.State == ModuleState.Destroyed;
        bool r = rightTrack && rightTrack.State == ModuleState.Destroyed;

        // ===== 운전수 =====
        bool dead = driverM && driverM.State == ModuleState.Destroyed;

        driver.SetTrackState(l, r);
        driver.SetMobilityModuleState(canMove: !imm, maxSpeedMul01: imm ? 0f : mul);
        driver.SetDriverState(dead, driverM.Hp01);
        
       
    }
}
