using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TankMobilityEffects : MonoBehaviour
{

    private DriverController driver;
    [SerializeField] private bool player;
    [SerializeField] private ModuleDamageController engine;        
    [SerializeField] private ModuleDamageController transmission;
    [SerializeField] private ModuleDamageController leftTrack;
    [SerializeField] private ModuleDamageController rightTrack;

    [SerializeField, Range(0f, 1f)] private float minMul = 0.15f; //

    private void Awake()
    {
        if(player) driver = GetComponent<DriverController>();
      
    }
    private void Update()
    {
        if (!driver) return;

        //엔진/ 트랜스미션 상태 체크
        bool imm = (engine && engine.State == ModuleState.Destroyed) || (transmission && transmission.State == ModuleState.Destroyed);

        float e = engine ? engine.Hp01 : 1f;
        float t = transmission ? transmission.Hp01 : 1f;

        float mul = Mathf.Min(e, t);
        mul = Mathf.Max(minMul, mul);
        //무한 궤도 상태 체크
        bool l = leftTrack && leftTrack.State == ModuleState.Destroyed;
        bool r = rightTrack && rightTrack.State == ModuleState.Destroyed;

        if (player)
        {
            driver.SetTrackDestroyed(l, r);
            driver.SetMobilityState(canMove: !imm, maxSpeedMul01: imm ? 0f : mul);
        }
       
    }
}
