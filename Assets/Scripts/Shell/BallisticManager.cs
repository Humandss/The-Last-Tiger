using System;
using System.Collections;
using UnityEngine;

public enum ImpactType
{
    Penetration,
    Ricochet,
    NoPenetration,
}

public class BallisticManager : MonoBehaviour
{
    [Header("Refs")]
    private ShellSoundController shellSoundController;
    [SerializeField] private ShellData shell;
    [SerializeField] private GameObject explosionPrefab;

    [Header("Impact Decals")]
    [SerializeField] private GameObject decalPenetrationPrefab;
    [SerializeField] private GameObject decalRicochetPrefab;
    [SerializeField] private GameObject decalNoPenPrefab;
    [SerializeField] private float decalSurfaceOffset = 0.05f;

    [Header("Decal Caliber Scaling")]
    [Tooltip("데칼 스케일 = 구경(mm) × 이 값")]
    [SerializeField] private float decalCaliberFactor = 0.04f;

    [Header("Hit Layers")]
    [SerializeField] private LayerMask worldMask;
    [SerializeField] private LayerMask moduleMask;
    [SerializeField] private LayerMask hitMeshMask;
    [SerializeField] private LayerMask armorZoneMask;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask fragmentHitMask;
    [SerializeField] private LayerMask occluderMask;

    [Header("Bullet Value")]
    private int id = 0;
    private static int idSeq = 0;
    private Vector3 velocity;
    private Vector3 pos;
    private Vector3 prevPos;
    private Vector3 dir;
    private float refArea;
    private float flightTime;
    private int ricochetChance = 0;
    private float speed;
    private float pen;
    private bool isPenetratingTerrain = false;
    private float radius;

    [Header("World")]
    private float airDensity = 1.225f;
    private Vector3 windWorld = Vector3.zero;
    private float k;
    private const float exit = 0.04f;
    private const float enter = 0.02f;

    [Header("Penetration")]
    [SerializeField] private float penetrationSpeedLoss = 0.25f;
    [SerializeField] private float minSpeedAfterPen = 30f;
    [SerializeField] private int maxPenetrations = 3;
    [SerializeField] private int maxImpactChainPerStep = 4;
    private int penCount = 0;

    [Header("Module Hit")]
    [SerializeField] private float moduleDirectDamage = 35f;

    [Header("Hit Cast")]
    [SerializeField] private float zoneSearchRadius = 0.35f;
    [SerializeField] private float minCosForArmor = 0.05f;

    [Header("Object Penetration (thickness based)")]
    [SerializeField] private float maxDist = 20f;
    [SerializeField] private float probeDist = 0.4f;
    [SerializeField] private float penCostPerMeter = 80f;
    [SerializeField] private float speedLossPerCost = 0.002f;
    [SerializeField] private float minThicknessM = 0.01f;

    [Header("Shell Info")]
    [SerializeField] private float lifeTimeAfterRicochet = 3f;
    [SerializeField] private int maxRicochet = 2;
    [SerializeField] private float aiReactionRadius = 40.0f;
    [SerializeField] private float radiusAt1Kg = 6.0f;
    [SerializeField] private int raysAt1Kg = 120;
    [SerializeField] private float baseFragDamage = 100.0f;
    public bool isPlayerShell = false;
    private float fuzeTimer = 0.0f;
    private bool fuzeArmed;
    private bool hasFuzeOrigin;
    private Vector3 fuzeOrigin;
    [SerializeField] private float fuzeOriginNudge = 0.1f;
    private bool isInitialized = false;
    private float lifeTime = 0f;

    public static event Action OnEnemyPenetrated;
    public static event Action OnEnemyRicocheted;   
    public static event Action OnEnemyNoPenetration; 

    public int Id => id;

#if true
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private Light tracerLight;
    [SerializeField] private float igniteDelay = 0.08f;
    [SerializeField] private float burnTime = 2.0f;

    void Awake()
    {
        shellSoundController = GetComponent<ShellSoundController>();
        if (trail) trail.emitting = false;
        if (tracerLight) tracerLight.enabled = false;
        fuzeArmed = false;
    }

    void OnEnable()
    {
        if (trail) { trail.emitting = false; trail.Clear(); }
        if (tracerLight) tracerLight.enabled = false;
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
        fuzeArmed = false;
        fuzeTimer = 0f;
        hasFuzeOrigin = false;
        fuzeOrigin = Vector3.zero;

        id = System.Threading.Interlocked.Increment(ref idSeq);
        isPenetratingTerrain = false;
        ricochetChance = 0;
        penCount = 0;
        flightTime = 0.0f;
        pos = position;
        prevPos = pos;
        dir = direction.normalized;
        pen = shell.penetrationPower;
        radius = (shell.caliber * 0.001f) * 0.5f;
        velocity = dir * shell.muzzleVelocity;
        lifeTime = shell.lifeTime;
        float invMass = 1.0f / Mathf.Max(1e-6f, shell.projectileMass);
        float r = Mathf.Max(1e-6f, shell.caliber * 0.001f) * 0.5f;
        refArea = Mathf.PI * r * r * shell.refAreaScale;
        k = 0.5f * airDensity * shell.dragCoeff * refArea * invMass;
        isInitialized = true;
    }

    private void FixedUpdate()
    {
        if (!isInitialized) return;

        float dt = Time.fixedDeltaTime;
        flightTime += dt;
        if (flightTime > lifeTime)
        {
            PoolManager.Instance.Return(gameObject);
            return;
        }

        prevPos = pos;

        Vector3 vRel = velocity - windWorld;
        speed = vRel.magnitude + 1e-6f;
        Vector3 g = Physics.gravity + (-k * vRel * speed);

        velocity += g * dt;
        pos += velocity * dt;

        NotifyNearbyAI(prevPos, pos);
        HandleImpactSegment(prevPos, pos, maxImpactChainPerStep);

        transform.position = pos;
        TickFuze(dt);
    }

    private void HandleImpactSegment(Vector3 start, Vector3 end, int depth)
    {
        if (depth <= 0) return;

        Vector3 seg = end - start;
        float segLen = seg.magnitude;
        if (segLen <= 1e-6f) return;

        Vector3 segDir = seg / segLen;

        if (!Physics.SphereCast(start, radius, segDir, out RaycastHit hit, segLen, worldMask, QueryTriggerInteraction.Ignore))
            return;

        int layerBit = 1 << hit.collider.gameObject.layer;
        float cosToNormal = Mathf.Clamp(Vector3.Dot(-segDir, hit.normal.normalized), -1f, 1f);
        float angleToNormal = Mathf.Acos(cosToNormal) * Mathf.Rad2Deg;

        // 카메라 흔들림 — 플레이어 탱크 hit이면 (지면 제외) 항상 발동
        // 모듈/장갑/월드 분기와 무관하게 통합 처리
        if (hit.collider.transform.root.CompareTag("Player") &&
            (groundMask.value & layerBit) == 0)
        {
            TryTriggerHitShake(segDir);
        }

        if ((moduleMask.value & layerBit) != 0 && HandleModuleHit(hit, segDir, segLen, depth))
            return;

        SpawnExplosion(hit);
        shellSoundController.PlayHit(hit.point);

        if ((groundMask.value & layerBit) != 0)
        {
            HandleGroundHit(hit, segDir, angleToNormal);
            return;
        }

        if ((hitMeshMask.value & layerBit) != 0)
        {
            ArmorManager zone = FindZone(hit.point);
            if (zone != null)
            {
                bool penetrated = HandleArmorHit(hit, segDir, cosToNormal, angleToNormal, zone);
                if (penetrated)
                    ContinueAfterPenetration(hit, segDir, segLen, depth);
                return;
            }

            bool worldPassed = HandleWorldHit(hit, segDir, angleToNormal);
            if (worldPassed)
                ContinueAfterPenetration(hit, segDir, segLen, depth);
            return;
        }

        bool passed = HandleWorldHit(hit, segDir, angleToNormal);
        if (passed)
            ContinueAfterPenetration(hit, segDir, segLen, depth);
    }

    private void ContinueAfterPenetration(RaycastHit hit, Vector3 segDir, float segLen, int depth)
    {
        float remain = segLen - hit.distance;
        if (remain <= 1e-4f) return;

        Vector3 newStart = pos;
        Vector3 newEnd = newStart + segDir * remain;
        HandleImpactSegment(newStart, newEnd, depth - 1);
    }

    private bool HandleModuleHit(RaycastHit hit, Vector3 segDir, float segLen, int depth)
    {
        ModuleDamageController module = hit.collider.GetComponentInParent<ModuleDamageController>();
        if (module == null) return false;

        module.TakeDamage(moduleDirectDamage, DamageType.DirectHit);

        bool penetrated = HandleObjectPenetration(hit, segDir);
        if (!penetrated) return true;

        ContinueAfterPenetration(hit, segDir, segLen, depth);
        return true;
    }

    private ArmorManager FindZone(Vector3 point)
    {
        Collider[] cols = Physics.OverlapSphere(point, zoneSearchRadius, armorZoneMask, QueryTriggerInteraction.Collide);
        if (cols == null || cols.Length == 0) return null;

        ArmorManager best = null;
        float bestD = float.MaxValue;

        for (int i = 0; i < cols.Length; i++)
        {
            ArmorManager z = cols[i].GetComponentInParent<ArmorManager>();
            if (!z) continue;

            Vector3 cp = cols[i].ClosestPoint(point);
            float d = (cp - point).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = z;
            }
        }

        return best;
    }

    private void TryTriggerHitShake(Vector3 segDir)
    {
        if (CameraController.cameraInstance == null) return;

        // 속도비 (현재 속도 / 머즐 속도) — 0~1
        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(1e-3f, shell.muzzleVelocity));

        // 무게비 — 30kg 기준 정규화 (이전엔 1kg+ 다 1.0이라 변별력 없었음)
        float massRatio = Mathf.Clamp01(shell.projectileMass / 30f);

        float intensity = speedRatio * massRatio;
        CameraController.cameraInstance.TriggerHitShake(segDir, intensity);
    }

    private bool HandleArmorHit(RaycastHit hit, Vector3 segDir, float cosToNormal, float angleToNormal, ArmorManager zone)
    {
        float ricTh = zone.GetRicochetThresholdDeg(shell.baseRicochetAngleDeg);
        if (shell.canRicochet && angleToNormal >= ricTh)
        {
            Debug.Log($"[RICOCHET] zone={zone.zoneName} angle={angleToNormal:0.0} threshold={ricTh:0.0}");
            SpawnImpactDecal(hit, ImpactType.Ricochet);
            HandleRicochet(hit, segDir);
            if (isPlayerShell) OnEnemyRicocheted?.Invoke();

            return false;
        }

        float baseArmorMm = zone.GetBaseArmorMm();
        float effectiveMm = baseArmorMm / Mathf.Max(minCosForArmor, cosToNormal);

        float vNow = velocity.magnitude;
        float v0 = Mathf.Max(1e-3f, shell.muzzleVelocity);
        float scale = Mathf.Pow(Mathf.Clamp01(vNow / v0), shell.penVelocityExponent);
        scale = Mathf.Max(shell.minPenScale, scale);

        float penMm = shell.penetrationPower * scale;

        if (penMm >= effectiveMm)
        {
            float penLeft = penMm - effectiveMm;
            Debug.Log($"[PEN] zone={zone.zoneName}, original pen ={pen:0}, effective pen={penMm:0}, original armor ={baseArmorMm:0}, eff={effectiveMm:0} angleN={angleToNormal:0}");
            SpawnImpactDecal(hit, ImpactType.Penetration);
            if (isPlayerShell) OnEnemyPenetrated?.Invoke();
            if (shell.canExplode)
                TryArmFuzeAfterPen(effectiveMm, hit.point, segDir);

            return HandleArmorPenetration(hit, segDir, penMm, penLeft);
        }

        Debug.Log($"[NO PEN] zone={zone.zoneName}, original pen ={pen:0}, effective pen={penMm:0} eff={effectiveMm:0} angleN={angleToNormal:0}");
        SpawnImpactDecal(hit, ImpactType.NoPenetration);
        if (isPlayerShell) OnEnemyNoPenetration?.Invoke();
        if (shell.type == AmmoType.HE) ExplodeNow();
        else PoolManager.Instance.Return(gameObject);
        return false;
    }

    private bool HandleWorldHit(RaycastHit hit, Vector3 segDir, float angleToNormal)
    {
        if (isPenetratingTerrain) return false;

        int layerBit = 1 << hit.collider.gameObject.layer;

        if ((groundMask.value & layerBit) != 0)
        {
            HandleGroundHit(hit, segDir, angleToNormal);
            return false;
        }

        return HandleObjectPenetration(hit, segDir);
    }

    private void HandleRicochet(RaycastHit hit, Vector3 dirN)
    {
        if (ricochetChance >= maxRicochet) { PoolManager.Instance.Return(gameObject); return; }

        lifeTime = flightTime + lifeTimeAfterRicochet;

        if(isPlayerShell) shellSoundController.PlayRicochet(hit.point);

        Vector3 recochetAngle = Vector3.Reflect(dirN, hit.normal).normalized;
        Vector3 axis = Vector3.Cross(hit.normal, recochetAngle);
        axis.Normalize();

        float maxRecochetAngle = Mathf.Lerp(0.0f, 6.0f, Mathf.Clamp01(shell.randomRicochetAngle));
        float angle = UnityEngine.Random.Range(-maxRecochetAngle, maxRecochetAngle);
        recochetAngle = (Quaternion.AngleAxis(angle, axis) * recochetAngle).normalized;

        float aterRicochetSpeed = speed * shell.afterRicochetEnergyPercent;
        velocity = recochetAngle * aterRicochetSpeed;
        pos = hit.point + hit.normal * exit;
        transform.position = pos;
        ricochetChance++;
        
    }

    private bool HandleArmorPenetration(RaycastHit hit, Vector3 segDir, float penBefore, float penLeft)
    {
        penCount++;
        if (penCount > maxPenetrations)
        {
            PoolManager.Instance.Return(gameObject);
            return false;
        }

        Vector3 dirN = segDir.sqrMagnitude > 1e-8f ? segDir.normalized : velocity.normalized;
        if (dirN.sqrMagnitude < 1e-8f)
        {
            PoolManager.Instance.Return(gameObject);
            return false;
        }

        pos = hit.point + dirN * enter;
        prevPos = pos;
        transform.position = pos;

        pen = penLeft;
        if (pen <= 0f)
        {
            if (shell.type == AmmoType.HE) ExplodeNow();
            else PoolManager.Instance.Return(gameObject);
            return false;
        }

        float keep = penBefore <= 1e-3f ? 0.5f : Mathf.Clamp01(penLeft / penBefore);
        float vScale = Mathf.Sqrt(Mathf.Max(0.05f, keep));
        vScale *= 1f - Mathf.Clamp01(penetrationSpeedLoss);

        velocity *= vScale;
        speed = velocity.magnitude;

        if (speed < minSpeedAfterPen)
        {
            if (shell.type == AmmoType.HE) ExplodeNow();
            else PoolManager.Instance.Return(gameObject);
            return false;
        }

        return true;
    }

    private bool HandleObjectPenetration(RaycastHit hit, Vector3 dirN)
    {
        if (dirN.sqrMagnitude < 1e-8f)
        {
            PoolManager.Instance.Return(gameObject);
            return false;
        }

        dirN.Normalize();

        Vector3 startInside = hit.point + dirN * enter;
        if (!TryFindExitPoint(hit.collider, startInside, dirN, out RaycastHit exitHit))
        {
            Debug.Log("[ObjPen] exit not found -> destroy");
            PoolManager.Instance.Return(gameObject);
            return false;
        }

        float thicknessM = Vector3.Distance(exitHit.point, hit.point);

        if (thicknessM < minThicknessM)
        {
            pos = hit.point + dirN * exit;
            prevPos = pos;
            transform.position = pos;
            return true;
        }

        float cost = thicknessM * penCostPerMeter;
        pen -= cost;

        if (pen <= 0f)
        {
            Debug.Log($"[ObjPen] stop thickness={thicknessM:F3} cost={cost:F1} pen<=0");
            if (shell.type == AmmoType.HE) ExplodeNow();
            else PoolManager.Instance.Return(gameObject);
            return false;
        }

        float loss01 = Mathf.Clamp01(cost * speedLossPerCost);
        speed *= 1f - loss01;
        velocity = dirN * speed;

        if (speed < minSpeedAfterPen)
        {
            if (shell.type == AmmoType.HE) ExplodeNow();
            else PoolManager.Instance.Return(gameObject);
            return false;
        }

        pos = exitHit.point + dirN * exit;
        prevPos = pos;
        transform.position = pos;

        Debug.Log($"[ObjPen] pass thickness={thicknessM:F3}m cost={cost:F1} pen={pen:F1} speed={speed:F1}");
        return true;
    }

    private bool TryFindExitPoint(Collider col, Vector3 entryPoint, Vector3 dirN, out RaycastHit exitHit)
    {
        exitHit = default;
        if (col == null) return false;
        if (dirN.sqrMagnitude < 1e-8f) return false;

        dirN.Normalize();

        Vector3 startInside = entryPoint + dirN * enter;
        if (col.Raycast(new Ray(startInside, dirN), out exitHit, maxDist))
            return true;

        Vector3 probeStart = entryPoint + dirN * probeDist;
        if (col.Raycast(new Ray(probeStart, -dirN), out exitHit, probeDist * 1.5f))
            return true;

        return false;
    }

    private void TryArmFuzeAfterPen(float traversedArmorMm, Vector3 originCandidate, Vector3 shotDir)
    {
        if (!shell.canExplode || fuzeArmed) return;

        if (traversedArmorMm < shell.fuzeSensitivity)
        {
            Debug.Log($"[APHE] FUZE NOT ARMED (thin) {traversedArmorMm:0.0}mm < {shell.fuzeSensitivity:0.0}mm");
            return;
        }

        Vector3 dirN = shotDir.sqrMagnitude > 1e-8f ? shotDir.normalized : velocity.normalized;
        if (dirN.sqrMagnitude < 1e-8f) dirN = transform.forward;

        float nudge = Mathf.Max(fuzeOriginNudge, enter + 0.04f);
        fuzeOrigin = originCandidate + dirN * nudge;
        hasFuzeOrigin = true;

        fuzeArmed = true;
        fuzeTimer = Mathf.Max(0f, shell.fuzeDelay);

        Debug.Log($"[APHE] FUZE ARMED armor={traversedArmorMm:0.0}mm delay={fuzeTimer:0.000}s");
    }

    private void TickFuze(float dt)
    {
        if (!fuzeArmed || !shell.canExplode) return;

        fuzeTimer -= dt;
        if (fuzeTimer > 0f) return;

        ExplodeNow();
    }

    private void ExplodeNow()
    {
        Vector3 origin = hasFuzeOrigin ? fuzeOrigin : pos;

        ShellExplosion.ExplodeFragmentsFromTntGrams(
            origin: origin,
            tntGrams: shell.tntMass,
            occluderMask: occluderMask,
            damageMask: fragmentHitMask,
            baseDamageAtCenter: baseFragDamage,
            radiusAt1Kg: radiusAt1Kg,
            raysAt1Kg: raysAt1Kg,
            includeTriggers: true,
            debug: false
        );

        float explosionRadius = ShellExplosion.ComputeRadiusFromTntGrams(shell.tntMass, radiusAt1Kg);
        int candidates = Physics.OverlapSphere(origin, explosionRadius, fragmentHitMask, QueryTriggerInteraction.Collide).Length;
        bool insideOcc = Physics.CheckSphere(origin, 0.01f, occluderMask, QueryTriggerInteraction.Collide);
        _ = candidates;
        _ = insideOcc;

        PoolManager.Instance.Return(gameObject);
    }

    private void HandleGroundHit(RaycastHit hit, Vector3 dirN, float angleToNormal)
    {
        if (shell.canRicochet && angleToNormal >= shell.baseRicochetAngleDeg)
        {
            HandleRicochet(hit, dirN);
            return;
        }

        if (shell.type == AmmoType.HE) ExplodeNow();
        else PoolManager.Instance.Return(gameObject);
    }

    private void SpawnExplosion(RaycastHit hit)
    {
        if (explosionPrefab == null) return;

        GameObject go = PoolManager.Instance.Spawn(explosionPrefab, hit.point + hit.normal * enter, Quaternion.LookRotation(hit.normal));
        if (go == null) return;

        ParticleSystem ps = go.GetComponentInChildren<ParticleSystem>();
        float delay = ps ? ps.main.duration + ps.main.startLifetime.constantMax + 0.5f : 1.3f;
        PoolManager.Instance.ReturnDelayed(go, delay);
    }

    private void SpawnImpactDecal(RaycastHit hit, ImpactType type)
    {
        GameObject prefab = type switch
        {
            ImpactType.Penetration => decalPenetrationPrefab,
            ImpactType.Ricochet => decalRicochetPrefab,
            ImpactType.NoPenetration => decalNoPenPrefab,
            _ => null,
        };

        if (prefab == null) return;

        int layerBit = 1 << hit.collider.gameObject.layer;
        bool isValidSurface =
            (hitMeshMask.value & layerBit) != 0 ||
            (armorZoneMask.value & layerBit) != 0;

        if (!isValidSurface) return;

        Vector3 spawnPos = hit.point + hit.normal * decalSurfaceOffset;
        Quaternion rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
        rot *= Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

        GameObject decal = PoolManager.Instance.Spawn(prefab, spawnPos, rot);
        if (decal == null) return;

        // 1) 부모 연결 전에 월드 스케일 먼저 세팅
        //    (이 시점엔 부모 없으므로 localScale = worldScale)
        float caliberScale = shell.caliber * decalCaliberFactor;
        decal.transform.localScale = prefab.transform.localScale * caliberScale;

        // 2) 그 후 부모 연결 (worldPositionStays:true → 월드 스케일 보존)
        //    Unity가 부모 lossyScale을 고려해 localScale을 자동 계산하므로
        //    실제 화면상 크기는 1)에서 정한 값 그대로 유지됨
        decal.transform.SetParent(hit.collider.transform, worldPositionStays: true);

        // 버젯 매니저 등록 — 라이프타임 대신 버젯 초과 시 LRU 회수
        if (DecalBudgetManager.Instance != null)
            DecalBudgetManager.Instance.Register(decal);
    }

    private void NotifyNearbyAI(Vector3 from, Vector3 to)
    {
        if (!isPlayerShell) return;

        TankAIController[] controllers = FindObjectsByType<TankAIController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float radiusSqr = aiReactionRadius * aiReactionRadius;

        for (int i = 0; i < controllers.Length; i++)
        {
            TankAIController ai = controllers[i];
            if (ai == null) continue;

            float distSqr = DistancePointToSegmentSqr(ai.transform.position, from, to);
            if (distSqr > radiusSqr) continue;

            ai.OnNearbyShell(id, to);
        }
    }

    private static float DistancePointToSegmentSqr(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float denom = ab.sqrMagnitude;
        if (denom <= 1e-6f) return (point - a).sqrMagnitude;

        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / denom);
        Vector3 closest = a + ab * t;
        return (point - closest).sqrMagnitude;
    }
}
