using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount);
}

public class ShellExplosion : MonoBehaviour
{
    // g -> kg 변환
    public static float GramsToKg(float grams) => Mathf.Max(0.001f, grams) * 0.001f;

    // TNT kg -> blast radius(m)
    // 1kg 기준 반경을 radiusAt1Kg로 두고, 질량^(1/3)로 스케일
    public static float ComputeRadiusFromTntGrams(float tntGrams, float radiusAt1Kg = 6.0f)
    {
        float tntKg = GramsToKg(tntGrams);
        return radiusAt1Kg * Mathf.Pow(tntKg, 1f / 3f);
    }

    // TNT kg -> fragment ray count
    public static int ComputeFragmentRaysFromTntGrams(float tntGrams, int raysAt1Kg = 80)
    {
        float tntKg = GramsToKg(tntGrams);
        // 0.35 정도로 완만하게 증가
        float scale = Mathf.Pow(tntKg, 0.35f);
        return Mathf.Clamp(Mathf.RoundToInt(raysAt1Kg * scale), 24, 600);
    }

    /// <summary>
    /// TNT 질량(그램) 기반 파편 폭발. (프리팹 없이 레이캐스트로 파편 판정)
    /// - origin: 폭발 위치
    /// - tntGrams: TNT 질량 (g)
    /// - hitMask: 파편이 맞출 레이어
    /// - baseDamageAtCenter: 중심(0m)에서의 최대 피해량
    /// - radiusAt1Kg: 1kg TNT일 때 반경(m)
    /// - raysAt1Kg: 1kg TNT일 때 파편 레이 수
    /// </summary>
    public static void ExplodeFragmentsFromTntGrams(
        Vector3 origin,
        float tntGrams,
        LayerMask hitMask,
        float baseDamageAtCenter = 120f,
        float radiusAt1Kg = 6f,
        int raysAt1Kg = 80,
        bool debugRays = false,
        float debugTime = 0.25f)
    {
        float radius = ComputeRadiusFromTntGrams(tntGrams, radiusAt1Kg);
        int rays = ComputeFragmentRaysFromTntGrams(tntGrams, raysAt1Kg);

        // 같은 대상에 여러 번 맞는 걸 과딜로 만들기 싫으면 "최댓값 1회"로 처리
        Dictionary<IDamageable, float> bestHit = new Dictionary<IDamageable, float>();

        for (int i = 0; i < rays; i++)
        {
            Vector3 dir = Random.onUnitSphere;

            if (Physics.Raycast(origin, dir, out var hit, radius, hitMask, QueryTriggerInteraction.Ignore))
            {
                float dist01 = Mathf.Clamp01(hit.distance / radius);

                // 거리 감쇠(가까울수록 강): (1-d)^2 형태
                float dmg = baseDamageAtCenter * (1f - dist01) * (1f - dist01);

                var d = hit.collider.GetComponentInParent<IDamageable>();
                if (d != null)
                {
                    if (bestHit.TryGetValue(d, out float prev))
                        bestHit[d] = Mathf.Max(prev, dmg);
                    else
                        bestHit.Add(d, dmg);
                }

                if (debugRays) Debug.DrawLine(origin, hit.point, Color.red, debugTime);
            }
            else
            {
                if (debugRays) Debug.DrawRay(origin, dir * radius, Color.yellow, debugTime);
            }
        }

        foreach (var kv in bestHit)
            kv.Key.TakeDamage(kv.Value);

        Debug.Log($"[EXPLOSION] tnt={tntGrams:0.#}g radius={radius:0.00}m rays={rays}");
    }
}
