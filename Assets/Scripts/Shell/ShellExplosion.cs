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
        float debugTime = 0.25f,

         // 튜닝 파라미터(필요하면 조절)
        float startEpsilon = 0.06f,      // 레이 시작점을 살짝 밀어내는 값(미터)
        float minDamageFactor = 0.1f     // 아주 먼거리에도 최소 이 비율만큼은(원치 않으면 0)
        )
    {
        float radius = ComputeRadiusFromTntGrams(tntGrams, radiusAt1Kg);
        int rays = ComputeFragmentRaysFromTntGrams(tntGrams, raysAt1Kg);

        var qti = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        // 성능: 필요하면 여기서 "폭발 반경 내 damageable 후보"를 미리 모아두고,
        // RaycastAll 결과에서 d==null이면 스킵하는 식으로 최적화 가능.
        Dictionary<IDamageable, float> best = new Dictionary<IDamageable, float>();

        int rayHits = 0;        // 내부에 실제로 맞은 레이 수
        int rayBlocked = 0;     // 장갑에 막혀 끝난 레이 수
        int rayMiss = 0;        // 아무것도 안 맞은 레이 수

        // 레이 방향 샘플링: "균등 분포"로 (랜덤보다 hits=0 같은 운빨 줄어듦)
        for (int i = 0; i < rays; i++)
        {
            Vector3 dir = FibonacciSphereDirection(i, rays);
            Vector3 start = origin + dir * Mathf.Max(0f, startEpsilon);

            // occluder + damage 둘 다 맞춰보고, 정렬해서 "가장 먼저 만난 유효 대상" 처리
            int combinedMask = occluderMask.value | damageMask.value;

            RaycastHit[] hits = Physics.RaycastAll(start, dir, radius, combinedMask, qti);
            if (hits == null || hits.Length == 0)
            {
                rayMiss++;
                if (debug) Debug.DrawRay(start, dir * radius, Color.gray, debugTime);
                continue;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool done = false;

            for (int h = 0; h < hits.Length; h++)
            {
                var hit = hits[h];
                if (!hit.collider) continue;

                int layerBit = 1 << hit.collider.gameObject.layer;

                // 1) 먼저 damageMask 대상이면 데미지
                if ((damageMask.value & layerBit) != 0)
                {
                    var d = hit.collider.GetComponentInParent<IDamageable>();
                    if (d != null)
                    {
                        float dist = hit.distance; // start 기준 거리
                        float dist01 = Mathf.Clamp01(dist / radius);

                        // 거리 감쇠(너 기존과 동일한 2제곱)
                        float falloff = (1f - dist01);
                        float dmg = baseDamageAtCenter * falloff * falloff;

                        // 너무 멀면 완전 0 되는 게 싫으면 minDamageFactor로 바닥 깔기
                        if (minDamageFactor > 0f)
                            dmg = Mathf.Max(dmg, baseDamageAtCenter * minDamageFactor * 0.01f);

                        if (best.TryGetValue(d, out float prev))
                            best[d] = Mathf.Max(prev, dmg);
                        else
                            best.Add(d, dmg);

                        rayHits++;
                        done = true;

                        if (debug)
                            Debug.DrawLine(start, hit.point, Color.red, debugTime);

                        break;
                    }
                    // d가 null이면 그냥 계속 탐색 (레이어만 damageMask인데 damageable 없을 수 있음)
                }

                // 2) damage가 아니고 occluder면 막힘 처리하고 끝
                if ((occluderMask.value & layerBit) != 0)
                {
                    rayBlocked++;
                    done = true;

                    if (debug)
                        Debug.DrawLine(start, hit.point, Color.blue, debugTime);

                    break;
                }

                // 그 외 레이어면 계속
            }

            if (!done)
            {
                // hits는 있었는데 유효 damageable도 못 만나고, 막히지도 않은 애매한 케이스
                rayMiss++;
                if (debug) Debug.DrawRay(start, dir * radius, Color.gray, debugTime);
            }
        }

        // 최종 피해 적용 (1 대상 1회, 최대값)
        foreach (var kv in best)
            kv.Key.TakeDamage(kv.Value, DamageType.Fragment);

        Debug.Log($"[EXPLOSION-RAYS] tnt={tntGrams:0.#}g radius={radius:0.00}m rays={rays} blocked={rayBlocked} rayHits={rayHits} uniqueTargets={best.Count}");
    }

    private static Vector3 FibonacciSphereDirection(int i, int n)
    {
        // n=1 같은 엣지 방지
        if (n <= 1) return Vector3.forward;

        // 0..1
        float t = (i + 0.5f) / n;

        // y: 1 -> -1
        float y = 1f - 2f * t;
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));

        // 골든 앵글
        float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
        float phi = goldenAngle * i;

        float x = Mathf.Cos(phi) * r;
        float z = Mathf.Sin(phi) * r;

        return new Vector3(x, y, z);
    }

}
