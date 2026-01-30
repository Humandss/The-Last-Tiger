using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

public class BallisticManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ShellData shell;

    [Header("Hit Layers")]
    [SerializeField] private LayerMask hitMeshMask;     // HitMesh 레이어만
    [SerializeField] private LayerMask armorZoneMask;   // ArmorZone 레이어만

    [Header("Bullet Value")]
    private int id = 0; // 총알 아이디
    private static int idSeq = 0;
    private Vector3 velocity; //벡터 속력
    private Vector3 pos; //현 위치
    private Vector3 prevPos; // 이전 위치
    private Vector3 dir; //총알 방향
    private float refArea; //총알 면적
    private float flightTime;
    private int ricochetChance = 0;
    private float speed; //총알 속도
    private float pen; //총알 관통력
    private bool isPenetratingTerrain = false;
    private float radius;

    [Header("World")]
    private float airDensity = 1.225f;
    private Vector3 windWorld = Vector3.zero;
    private float k; // 공기저항
    private const float exit = 0.004f;
    private const float enter = 0.002f;

    [Header("Hit Cast")]
    [SerializeField] private float zoneSearchRadius = 0.35f; // ArmorZone 찾는 반경(박스 크기에 맞춰 조절)
    [SerializeField] private float minCosForArmor = 0.05f;   // 유효두께 계산 시 분모 최소값

#if true// 탄 트레일 남기는 로직
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private Light tracerLight;
    [SerializeField] private float igniteDelay = 0.08f;
    [SerializeField] private float burnTime = 2.0f;

    void Awake()
    {
        if (trail) trail.emitting = false;
        if (tracerLight) tracerLight.enabled = false;

    }

    void OnEnable()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(igniteDelay);

        if (trail) trail.emitting = true;
        if (tracerLight) tracerLight.enabled = true;

        yield return new WaitForSeconds(burnTime);

        if (trail) trail.emitting = false;
        if (tracerLight) tracerLight.enabled = false;
    }
#endif

    public void Initialize(Vector3 position, Vector3 direction)
    {
        id = System.Threading.Interlocked.Increment(ref idSeq);
        isPenetratingTerrain = false;
        ricochetChance = 0;
        flightTime = 0.0f;
        pos = position;
        prevPos = pos;
        dir = direction.normalized;
        pen = shell.penetrationPower;
        radius = (shell.caliber * 0.001f) * 0.5f; // m
        velocity = dir * shell.muzzleVelocity;   // 초기 속도 

        float invMass = 1.0f / Mathf.Max(1e-6f, shell.projectileMass); // 1/중량

        float r = Mathf.Max(1e-6f, (shell.caliber * 0.001f)) * 0.5f; // m로 바꾸기
        refArea = Mathf.PI * r * r * shell.refAreaScale; // 단면적(m)

        k = 0.5f * airDensity * shell.dragCoeff * refArea * invMass;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        flightTime += dt;
        if (flightTime > shell.lifeTime) { Destroy(gameObject); return; }

        prevPos = pos;
        //바람 저항
        Vector3 vRel = velocity - windWorld;
        //숫자 0이 되지 않게끔
        speed = vRel.magnitude + 1e-6f;
        //중력 계수*공기저항
        Vector3 g = Physics.gravity + (-k * vRel * speed);

        //속도 및 포지션 변환
        velocity += g * dt;
        pos += velocity * dt;

        HandleImpact(prevPos);

        transform.position = pos;

        //Debug.Log($"ammo type ={ammo.name}, pos={pos}, Vector_velocity={velocity}, time={flightTime}, distance={(flightTime*velocity).z}");
    }

    private void HandleImpact(Vector3 prevPos)
    {

        Vector3 seg = pos - prevPos;
        float segLen = seg.magnitude;
        if (segLen <= 1e-6f) return;

        Vector3 segDir = seg / segLen;

        // 1) HitMesh 먼저 맞추기 (정확한 point/normal 얻기)
        if (Physics.SphereCast(prevPos, radius, segDir, out var hit, segLen, hitMeshMask, QueryTriggerInteraction.Ignore))
        {
            // 각도: 0=정면, 90=스침
            float cosToNormal = Mathf.Clamp(Vector3.Dot(-segDir, hit.normal.normalized), -1f, 1f);
            float angleToNormal = Mathf.Acos(cosToNormal) * Mathf.Rad2Deg;

            //장갑판정
            ArmorManager zone = FindZone(hit.point);
            if (zone != null)
            {
                ResolveArmorHit(hit, segDir, cosToNormal, angleToNormal, zone);
                return;
            }

            // 3) ArmorZone 없으면 월드 히트로 처리(혹은 그냥 파괴)
            ResolveWorldHit(hit, segDir, angleToNormal);
            return;
        }
    }
    private ArmorManager FindZone(Vector3 point)
    {
        var cols = Physics.OverlapSphere(point, zoneSearchRadius, armorZoneMask, QueryTriggerInteraction.Collide);
        if (cols == null || cols.Length == 0) return null;

        ArmorManager best = null;
        float bestD = float.MaxValue;

        for (int i = 0; i < cols.Length; i++)
        {
            var z = cols[i].GetComponentInParent<ArmorManager>();
            if (!z) continue;

            // ClosestPoint로 거리 측정(겹칠 때도 안정적)
            Vector3 cp = cols[i].ClosestPoint(point);
            float d = (cp - point).sqrMagnitude;
            if (d < bestD) { bestD = d; best = z; }
        }

        return best;
    }

    private void ResolveArmorHit(RaycastHit hit, Vector3 segDir, float cosToNormal, float angleToNormal, ArmorManager zone)
    {
        //도탄
        float ricTh = zone.GetRicochetThresholdDeg(shell.baseRicochetAngleDeg); // base + bonus
        if (angleToNormal >= ricTh)
        {
            HandleRicochet(hit, segDir);
            return;
        }

        // (B) 유효두께(mm) = baseArmor / cos
        float baseArmorMm = zone.GetBaseArmorMm();
        float effectiveMm = baseArmorMm / Mathf.Max(minCosForArmor, cosToNormal);

        // (C) 관통력 (지금은 pen 그대로 쓰고, 나중에 거리감쇠 붙이면 됨)
        float vNow = velocity.magnitude;
        float v0 = Mathf.Max(1e-3f, shell.muzzleVelocity);
        float scale = Mathf.Pow(Mathf.Clamp01(vNow / v0), shell.penVelocityExponent);
        scale = Mathf.Max(shell.minPenScale, scale);

        float penMm = shell.penetrationPower * scale;

        if (penMm >= effectiveMm)
        {
            Debug.Log($"[PEN] zone={zone.zoneName}, original pen ={pen:0}, effective pen={penMm:0}, original armor ={baseArmorMm:0}, eff={effectiveMm:0} angleN={angleToNormal:0}");
            // TODO: 관통 처리 (탄 계속 진행/내부 히트/에너지 감쇠)
            Destroy(gameObject);
        }
        else
        {
            Debug.Log($"[NO PEN] zone={zone.zoneName}, original pen ={pen:0}, effective pen={penMm:0} eff={effectiveMm:0} angleN={angleToNormal:0}");
            Destroy(gameObject);
        }
   
    }
    private void ResolveWorldHit(RaycastHit hit, Vector3 segDir, float angleToNormal)
    {
        if (isPenetratingTerrain) return;

        if (angleToNormal >= shell.baseRicochetAngleDeg && ricochetChance < 1)
            HandleRicochet(hit, segDir);
        else
            Destroy(gameObject);
    }
    private void HandleRicochet(RaycastHit hit, Vector3 dirN)
    {

        Vector3 recochetAngle = Vector3.Reflect(dirN, hit.normal).normalized;
        //도탄후 랜덤으로 도탄될 각 기준 정하기
        Vector3 axis = Vector3.Cross(hit.normal, recochetAngle);
        axis.Normalize();
        //도탄 됐을 경우 퍼질 수 있는 최대각
        float maxRecochetAngle = Mathf.Lerp(0.0f, 6.0f, Mathf.Clamp01(shell.randomRicochetAngle));
        float angle = UnityEngine.Random.Range(-maxRecochetAngle, maxRecochetAngle);
        //최종 도탄 앵글
        recochetAngle = (Quaternion.AngleAxis(angle, axis) * recochetAngle).normalized;
        //도탄 후 에너지
        float aterRicochetSpeed = speed * shell.afterRicochetEnergyPercent;
        //최종 계산
        velocity = recochetAngle * aterRicochetSpeed;
        pos = hit.point + hit.normal * exit;

        transform.position = pos;
        ricochetChance++;


    }
}
