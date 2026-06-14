using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DamageType { DirectHit, Fragment, DefaultFire, AmmoRack, AmmoFire }

public enum ModuleState { Healthy, Damaged, Destroyed }

public enum PartSide { Internal, External }

public enum ModuleType
{
    Engine,
    Transmission,
    Track,
    Gun,
    Breech,
    Loader,
    Gunner,
    Driver,
    Commander,
    MachineGunner,
    Ammo,
    FuelTank
    
}

public interface IDamageable
{
    void TakeDamage(float amount, DamageType type);
}

public class ModuleDamageController : MonoBehaviour, IDamageable
{
    [Header("Info")]
    [SerializeField] private ModuleType moduleType = ModuleType.Engine;
    [SerializeField] private PartSide side = PartSide.Internal;
    [SerializeField] private string partName = "Part";
    private ModuleManager mgr;
    [Header("HP")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float hp = 100f;

    [Header("State Thresholds")]
    [Range(0.05f, 0.99f)]
    [SerializeField] private float damagedThreshold01 = 0.6f;

    [Header("Multipliers")]
    [SerializeField] private float directMul = 4f;   // 직격 배수
    [SerializeField] private float fragMul = 1f;     // 파편 배수
    [SerializeField] private float defaultFireDam =1f;
    [SerializeField] private float ammoFireDam = 10f;
    [SerializeField] private float explosionDam = 150f;

    [Header("Tuning")]
    [SerializeField] private bool destroyObjectOnZero = false;

    public event Action<ModuleDamageController, float, DamageType> OnDamaged; // 대미지를 입을시
    public event Action<ModuleDamageController, ModuleState, ModuleState> OnStateChanged; //상태 변화시
    public event Action<ModuleDamageController, DamageType> OnHit; //상태, 대미지 무관 피격시

    public float MaxHp => maxHp;
    public float Hp => hp;
    public float Hp01 => (maxHp <= 1e-6f) ? 0f : Mathf.Clamp01(hp / maxHp);
    public string PartName => partName;
    public PartSide Side => side;
    public void BindManager(ModuleManager mgr) => this.mgr = mgr;

    void Awake()
    {
        if (string.IsNullOrWhiteSpace(partName))
            partName = gameObject.name;

        hp = Mathf.Clamp(hp, 0f, maxHp);
    }

    public void TakeDamage(float amount, DamageType type)
    {
        //피격 이벤트 호출
        OnHit?.Invoke(this, type);

        if (hp <= 0f) return;

        var prevState = State;
        float dmg;

        if (type == DamageType.DefaultFire) dmg = defaultFireDam;
        else if (type == DamageType.AmmoRack) dmg = explosionDam;
        else if (type == DamageType.AmmoFire) dmg = ammoFireDam;
        else
        {
            float mul = (type == DamageType.DirectHit) ? directMul : fragMul;
            dmg = Mathf.Max(0f, amount) * Mathf.Max(0f, mul);
        }

        hp = Mathf.Max(0f, hp - dmg);

        //Debug.Log($"[DMG] {partName} ({moduleType}) side={side}, type={type}, dmg={dmg:0.0} hp={hp:0.0}/{maxHp:0.0}");

        OnDamaged?.Invoke(this, dmg, type);

        var nextState = State;
        if (prevState != nextState)
        {
           // Debug.LogWarning($"[STATE] {partName} ({moduleType}) {prevState} -> {nextState}");
            OnStateChanged?.Invoke(this, prevState, nextState);

            if (destroyObjectOnZero && nextState == ModuleState.Destroyed)
                gameObject.SetActive(false);
        }

        if (dmg > 0.0f && (type != DamageType.DefaultFire && type !=DamageType.AmmoFire)) 
            mgr?.NotifyHitEvent();
    }

    public ModuleType Type => moduleType;

    public void RepairToFull()
    {
        RepairToHp(maxHp);
    }

    public void RepairToDamagedState()
    {
        float targetHp01 = Mathf.Clamp01(damagedThreshold01 * 0.9f);
        float targetHp = Mathf.Max(1f, maxHp * targetHp01);
        RepairToHp(targetHp);
    }

    public void RepairToHp(float targetHp)
    {
        float clampedTargetHp = Mathf.Clamp(targetHp, 0f, maxHp);
        if (clampedTargetHp <= hp)
            return;

        var prevState = State;
        hp = clampedTargetHp;
        var nextState = State;

        if (prevState != nextState)
            OnStateChanged?.Invoke(this, prevState, nextState);
    }


    public ModuleState State
    {
        get
        {
            if (hp <= 0.001f) return ModuleState.Destroyed;
            if (Hp01 < damagedThreshold01) return ModuleState.Damaged;
            return ModuleState.Healthy;
        }
    }
}
