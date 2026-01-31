using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

public class BallisticManager : MonoBehaviour
{

    [Header("Refs")]
    [SerializeField] private ShellData shell;

    [Header("Hit Layers")]
    [SerializeField] private LayerMask worldMask;
    [SerializeField] private LayerMask hitMeshMask;     // HitMesh 레이어만
    [SerializeField] private LayerMask armorZoneMask;   // ArmorZone 레이어만
    [SerializeField] private LayerMask groundMask;  // Ground만

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
    private const float exit = 0.04f;
    private const float enter = 0.02f;



    [Header("Penetration")]
    [SerializeField] private float penetrationSpeedLoss = 0.25f; // 관통 시 추가 속도 손실
    [SerializeField] private float minSpeedAfterPen = 30f;        // 너무 느려지면 소멸
    [SerializeField] private int maxPenetrations = 5;             // 몇 번까지 관통 허용
    private int penCount = 0;
    private bool skipImpactThisStep = false; // 관통 직후 같은 스텝(중복) 충돌 재검사 방지

    [Header("Hit Cast")]
    [SerializeField] private float zoneSearchRadius = 0.35f; // ArmorZone 찾는 반경
    [SerializeField] private float minCosForArmor = 0.05f;   // 유효두께 계산 시 분모 최소값

    [Header("Object Penetration (thickness based)")]
    [SerializeField] private float maxDist = 20f;   // 출구 탐색 최대 거리
    [SerializeField] private float probeDist = 0.4f;// 역방향 보완 탐색
    [SerializeField] private float penCostPerMeter = 80f;     // 1m 통과 시 pen 감소량(튜닝)
    [SerializeField] private float speedLossPerCost = 0.002f; // cost->속도 감쇠(튜닝)
    [SerializeField] private float minThicknessM = 0.01f;     // 엣지/오차 방지

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

        //관통중이라면->관통 직후 한 스텝 스킵
        if (!skipImpactThisStep)
            HandleImpact(prevPos);
        else
            skipImpactThisStep = false;

       // HandleImpact(prevPos);

        transform.position = pos;

        //Debug.Log($"ammo type ={ammo.name}, pos={pos}, Vector_velocity={velocity}, time={flightTime}, distance={(flightTime*velocity).z}");
    }

    private void HandleImpact(Vector3 prevPos)
    {

        Vector3 seg = pos - prevPos;
        float segLen = seg.magnitude;
        if (segLen <= 1e-6f) return;

        Vector3 segDir = seg / segLen;

        // 여기서 hitMeshMask가 아니라 worldMask로 잡는다
        if (!Physics.SphereCast(prevPos, radius, segDir, out var hit, segLen, worldMask, QueryTriggerInteraction.Ignore))
            return;

        float cosToNormal = Mathf.Clamp(Vector3.Dot(-segDir, hit.normal.normalized), -1f, 1f);
        float angleToNormal = Mathf.Acos(cosToNormal) * Mathf.Rad2Deg;

        int layerBit = 1 << hit.collider.gameObject.layer;

        // Ground면: 도탄/파괴
        if ((groundMask.value & layerBit) != 0)
        {
            HandleGroundHit(hit, segDir, angleToNormal);
            return;
        }

        // Tank HitMesh면: ArmorZone 찾기
        if ((hitMeshMask.value & layerBit) != 0)
        {
            var zone = FindZone(hit.point);
            if (zone != null)
            {
                HandleArmorHit(hit, segDir, cosToNormal, angleToNormal, zone);
                return;
            }
            HandleWorldHit(hit, segDir, angleToNormal);
            return;
        }

        // 기타 월드: 두께 관통
        HandleWorldHit(hit, segDir, angleToNormal);
    
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

            // ClosestPoint로 거리 측정->제일 가까운 아머 존을 찾아 삽입
            Vector3 cp = cols[i].ClosestPoint(point);
            float d = (cp - point).sqrMagnitude;
            if (d < bestD) { bestD = d; best = z; }
        }

        return best;
    }

    private void HandleArmorHit(RaycastHit hit, Vector3 segDir, float cosToNormal, float angleToNormal, ArmorManager zone)
    {
        //도탄
        float ricTh = zone.GetRicochetThresholdDeg(shell.baseRicochetAngleDeg); // base + bonus
        if (angleToNormal >= ricTh)
        {
            HandleRicochet(hit, segDir);
            return;
        }

        //유효두께(mm) 
        float baseArmorMm = zone.GetBaseArmorMm();
        float effectiveMm = baseArmorMm / Mathf.Max(minCosForArmor, cosToNormal);

        //관통력 (지금은 pen 그대로 쓰고, 나중에 거리감쇠 붙이면 됨)
        float vNow = velocity.magnitude;
        float v0 = Mathf.Max(1e-3f, shell.muzzleVelocity);
        float scale = Mathf.Pow(Mathf.Clamp01(vNow / v0), shell.penVelocityExponent);
        scale = Mathf.Max(shell.minPenScale, scale);

        float penMm = shell.penetrationPower * scale;

        if (penMm >= effectiveMm)
        {
            float penLeft = penMm - effectiveMm;
            Debug.Log($"[PEN] zone={zone.zoneName}, original pen ={pen:0}, effective pen={penMm:0}, original armor ={baseArmorMm:0}, eff={effectiveMm:0} angleN={angleToNormal:0}");
            HandlePenetration(hit, segDir, penMm, penLeft);
            return;
        }
        else
        {
            Debug.Log($"[NO PEN] zone={zone.zoneName}, original pen ={pen:0}, effective pen={penMm:0} eff={effectiveMm:0} angleN={angleToNormal:0}");
            Destroy(gameObject);
            return;
        }
   
    }
    private void HandleWorldHit(RaycastHit hit, Vector3 segDir, float angleToNormal)
    {
        if (isPenetratingTerrain) return;

        int layerBit = 1 << hit.collider.gameObject.layer;

        // Ground 레이어면 도탄/파괴(탄 데이터 그대로)
        if ((groundMask.value & layerBit) != 0)
        {
            HandleGroundHit(hit, segDir, angleToNormal);
            return;
        }

        // 그 외는 두께 기반 관통
        HandleObjectPenetration(hit, segDir);
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

    private void HandlePenetration(RaycastHit hit, Vector3 segDir, float penBefore, float penLeft)
    {
        penCount++;
        if (penCount > maxPenetrations)
        {
            Destroy(gameObject);
            return;
        }

        // 관통 후 위치를 살짝 안쪽으로 이동
        pos = hit.point + segDir * enter;
        prevPos = pos;
        transform.position = pos;

        // 속도 감쇠: 남은 관통력 비율로 에너지 깎는 느낌
        float keep = (penBefore <= 1e-3f) ? 0.5f : Mathf.Clamp01(penLeft / penBefore);

        // 에너지 ~ v^2 라고 보고 v는 sqrt(keep)로 줄이기
        float vScale = Mathf.Sqrt(Mathf.Max(0.05f, keep));

        // 추가 손실(장갑 통과로 파편/변형/요동)
        vScale *= (1f - Mathf.Clamp01(penetrationSpeedLoss));

        velocity *= vScale;

        // 너무 느려졌으면 제거
        if (velocity.magnitude < minSpeedAfterPen)
        {
            Destroy(gameObject);
            return;
        }

        // 남은 관통력은 다음 히트에 반영되도록 저장
        pen *= Mathf.Clamp01(penLeft / Mathf.Max(1e-3f, penBefore));

        // 4) 관통 직후 같은 스텝에 재충돌 방지
        skipImpactThisStep = true;

        // TODO: 여기서 내부 모듈/승무원 히트 처리도 붙일 수 있음(다음 단계)
    }
    private void HandleObjectPenetration(RaycastHit hit, Vector3 dirN)
    {
        if (dirN.sqrMagnitude < 1e-8f) { Destroy(gameObject); return; }
        dirN.Normalize();

        // ===== 출구 찾기 =====
        bool found = false;
        RaycastHit exitHit = default;

        Vector3 startInside = hit.point + dirN * enter;

        // 1) 정방향
        if (hit.collider.Raycast(new Ray(startInside, dirN), out exitHit, maxDist))
        {
            found = true;
        }
        else
        {
            // 2) 역방향 보완
            Vector3 probeStart = hit.point + dirN * probeDist;
            if (hit.collider.Raycast(new Ray(probeStart, -dirN), out exitHit, probeDist * 1.5f))
            {
                found = true;

                // (선택) 역방향 점이 입구 표면일 수 있으니, 다시 정방향 재시도하고 싶으면 주석 해제
                // var retry = new Ray(exitHit.point + dirN * enter, dirN);
                // if (hit.collider.Raycast(retry, out var exitHit2, maxDist)) exitHit = exitHit2;
            }
        }

        if (!found)
        {
            Debug.Log("[ObjPen] exit not found -> destroy");
            Destroy(gameObject);
            return;
        }

        float thicknessM = Vector3.Distance(exitHit.point, hit.point);

        // 엣지/얇은 스침은 그냥 밖으로 빼고 계속 진행(또는 파괴)
        if (thicknessM < minThicknessM)
        {
            pos = hit.point + dirN * exit;
            prevPos = pos;
            transform.position = pos;
            return;
        }

        // ===== 비용(두께 기반) =====
        float cost = thicknessM * penCostPerMeter;
        pen -= cost;

        if (pen <= 0f)
        {
            Debug.Log($"[ObjPen] stop thickness={thicknessM:F3} cost={cost:F1} pen<=0");
            Destroy(gameObject);
            return;
        }

        // ===== 속도 감쇠 =====
        float loss01 = Mathf.Clamp01(cost * speedLossPerCost);
        speed *= (1f - loss01);
        velocity = dirN * speed;

        if (speed < minSpeedAfterPen)
        {
            Destroy(gameObject);
            return;
        }

        // ===== 출구로 이동 =====
        pos = exitHit.point + dirN * exit;
        prevPos = pos;
        transform.position = pos;

        // 같은 스텝 재충돌 방지 플래그(너가 쓰고 있으면)
        // skipImpactThisStep = true;

        Debug.Log($"[ObjPen] pass thickness={thicknessM:F3}m cost={cost:F1} pen={pen:F1} speed={speed:F1}");
    }
    private void HandleGroundHit(RaycastHit hit, Vector3 dirN, float angleToNormal)
    {
        if (angleToNormal >= shell.baseRicochetAngleDeg && ricochetChance < 2)
        {
            HandleRicochet(hit, dirN);
            return;
        }

        Destroy(gameObject);
    }
}
