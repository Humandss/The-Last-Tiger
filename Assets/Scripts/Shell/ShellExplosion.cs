using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShellExplosion
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
        LayerMask occluderMask,
        LayerMask damageMask,
        float baseDamageAtCenter = 120f,
        float radiusAt1Kg = 6f,
        int raysAt1Kg = 80,
        bool includeTriggers = true,
        bool debug = false,
        float debugTime = 0.25f)
    {
        float radius = ComputeRadiusFromTntGrams(tntGrams, radiusAt1Kg);

        var qti = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        // 1) 폭발 반경 안의 모듈 콜라이더 후보 수집
        Collider[] cols = Physics.OverlapSphere(origin, radius, damageMask, qti);
        if (cols == null || cols.Length == 0)
        {
            Debug.Log($"[EXPLOSION] tnt={tntGrams:0.#}g radius={radius:0.00}m modules=0 (mask?)");
            return;
        }

        // 2) 같은 대상(모듈/크루) 여러 콜라이더 있으면 1회만 적용(최대 데미지)
        Dictionary<IDamageable, float> best = new Dictionary<IDamageable, float>();
        int considered = 0;
        int blocked = 0;

        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (!col) continue;

            // IDamageable은 보통 콜라이더의 부모에 붙어있음
            var d = col.GetComponentInParent<IDamageable>();
            if (d == null) continue;

            // 가장 가까운 지점으로 “직선” 확인 (파편이 그쪽으로 날아간다고 가정)
            Vector3 target = col.ClosestPoint(origin);

            // 거리 기반 감쇠
            float dist = Vector3.Distance(origin, target);
            float dist01 = Mathf.Clamp01(dist / radius);
            float dmg = baseDamageAtCenter * (1f - dist01) * (1f - dist01);

            considered++;

            // 3) 가림 체크: origin -> target 사이에 장갑/히트메쉬가 먼저 있으면 피해 X
            //    단, 자기 자신(모듈 콜라이더)이 occluder에 포함되어 있으면 막힐 수 있으니 occluderMask에 모듈 레이어는 넣지 마
            if (Physics.Linecast(origin, target, out var blockHit, occluderMask, qti))
            {
                blocked++;
                if (debug) Debug.DrawLine(origin, blockHit.point, Color.blue, debugTime);
                continue;
            }

            if (debug) Debug.DrawLine(origin, target, Color.red, debugTime);

            if (best.TryGetValue(d, out float prev))
                best[d] = Mathf.Max(prev, dmg);
            else
                best.Add(d, dmg);
        }

        foreach (var kv in best)
            kv.Key.TakeDamage(kv.Value);

        Debug.Log($"[EXPLOSION] tnt={tntGrams:0.#}g radius={radius:0.00}m modules={cols.Length} considered={considered} blocked={blocked} hits={best.Count}");
    }
}
