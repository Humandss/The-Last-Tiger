using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ArmorMaterial
{
    RHA,        // 압연 균질강(차체)
    Cast,       // 주강(포탑)
    Spaced,     // 공간장갑/궤도/스커트 같은 느낌
}

public class ArmorManager : MonoBehaviour
{
    [Header("Identity")]
    public string zoneName = "HullFront";

    [Header("Armor (mm)")]
    [Tooltip("기본 장갑 두께(mm)")]
    public float armorMm = 45f;

    [Header("Material")]
    public ArmorMaterial material = ArmorMaterial.RHA;

    [Header("Ricochet")]
    [Tooltip("도탄 임계각 보정")]
    public float ricochetBonusDeg = 0f;

    [Header("Debug")]
    public bool drawGizmo = true;
    public Color gizmoColor = new Color(0f, 1f, 1f, 0.25f);

    /// <summary>
    /// 관통 계산에 쓸 '기본 장갑(mm)' (형상/재질 보정 포함)
    /// </summary>
    public float GetBaseArmorMm()
    {
        float a = Mathf.Max(0f, armorMm);
        float f = Mathf.Max(0.01f, MaterialFactor(material));
        return a * f;
    }

    private float MaterialFactor(ArmorMaterial m) => m switch
    {
        ArmorMaterial.RHA => 1.00f,
        ArmorMaterial.Cast => 0.95f,
        ArmorMaterial.Spaced => 1.05f,
        _ => 1.0f
    };

    /// <summary>
    /// 도탄 임계각(0=정면, 90=스침) 기본값에 보정을 더해 최종값 반환
    /// </summary>
    public float GetRicochetThresholdDeg(float baseThresholdDeg)
    {
        return baseThresholdDeg + ricochetBonusDeg;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        armorMm = Mathf.Max(0.0f, armorMm);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        Gizmos.color = gizmoColor;

        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
    }
#endif
}
