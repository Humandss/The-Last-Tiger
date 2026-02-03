using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.InputManagerEntry;

public enum DamageType { DirectHit, Fragment }

public enum PartSide { Internal, External }
public interface IDamageable
{
    void TakeDamage(float amount);
}

public class ModuleDamageController : MonoBehaviour, IDamageable
{
    [Header("Info")]
    [SerializeField] private PartSide side = PartSide.Internal;
    [SerializeField] private string partName = "Part";

    [Header("HP")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float hp = 100f;

    [Header("Multipliers")]
    [SerializeField] private float directMul = 1.0f;   // 직격 배수
    [SerializeField] private float fragMul = 1.0f;     // 파편 배수
    [Header("Tuning")]
    [SerializeField] private bool destroyObjectOnZero = false;

    void Awake()
    {
        if (string.IsNullOrWhiteSpace(partName))
            partName = gameObject.name;

        hp = Mathf.Clamp(hp, 0f, maxHp);
    }

    public void TakeDamage(float amount)
    {
        if (hp <= 0f) return;

        float dmg = Mathf.Max(0f, amount) * Mathf.Max(0f, fragMul);
        hp -= dmg;

        Debug.Log($"[DMG] {partName} side={side}, dmg={dmg:0.0} hp={hp:0.0}/{maxHp:0.0}");
        if (hp > 0f) return;

        hp = 0f;
        Debug.LogWarning($"[DESTROYED] {partName} ({side})");

    }
}
