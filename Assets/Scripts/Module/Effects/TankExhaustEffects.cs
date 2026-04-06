using UnityEngine;

/// <summary>
/// 탱크 배기 연기 이펙트.
/// exhaustPrefab을 배기관 위치에 스폰하고 상태에 따라 파티클을 제어합니다.
///
/// 상태별 동작:
///   공회전    → 옅은 흰/회색 연기, 낮은 배출량
///   가속      → 진한 연기 + 배출량 증가
///   엔진 손상 → 검은 연기, 배출량 증가
///   엔진 파괴 → 배출 정지
/// </summary>
public class TankExhaustEffects : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject       exhaustPrefab;
    [SerializeField] private Transform[]      exhaustPoints;
    [SerializeField] private DriverController driverController;
    [SerializeField] private ModuleManager    moduleManager;

    [Header("공회전")]
    [SerializeField] private float idleEmissionRate = 4f;
    [SerializeField] private Color idleColor        = new Color(0.80f, 0.80f, 0.80f, 0.35f);
    [SerializeField] private float idleStartSize    = 0.4f;
    [SerializeField] private float idleStartSpeed   = 0.6f;

    [Header("가속")]
    [SerializeField] private float throttleEmissionRate = 18f;
    [SerializeField] private Color throttleColor        = new Color(0.40f, 0.40f, 0.40f, 0.60f);
    [SerializeField] private float throttleStartSize    = 0.7f;
    [SerializeField] private float throttleStartSpeed   = 1.4f;

    [Header("엔진 손상")]
    [SerializeField] private float damagedEmissionRate = 24f;
    [SerializeField] private Color damagedColor        = new Color(0.10f, 0.10f, 0.10f, 0.80f);
    [SerializeField] private float damagedStartSize    = 0.9f;
    [SerializeField] private float damagedStartSpeed   = 1.8f;

    [Header("전환 속도")]
    [SerializeField] private float colorLerpSpeed    = 3f;
    [SerializeField] private float emissionLerpSpeed = 6f;
    [SerializeField] private float sizeLerpSpeed     = 3f;

    [Tooltip("이 스로틀 값 이상이면 가속 상태로 판정")]
    [SerializeField] private float throttleThreshold = 0.15f;

    // ──────────────────────────────────────────────
    private enum ExhaustState { Idle, Throttle, Damaged, Dead }

    private ExhaustState _state = ExhaustState.Idle;
    private bool _dead;
    private bool _poolReturned; // OnTankDestroyed에서 이미 반환했으면 OnDestroy 중복 방지

    private ParticleSystem[]                _psList;
    private ParticleSystem.EmissionModule[] _emissions;
    private ParticleSystem.MainModule[]     _mains;

    private float _curEmission;
    private Color _curColor;
    private float _curSize;
    private float _curSpeed;

    // ──────────────────────────────────────────────
    private void Start()
    {
        if (exhaustPrefab == null)
        {
            Debug.LogWarning("[TankExhaustEffects] exhaustPrefab이 할당되지 않았습니다.");
            enabled = false;
            return;
        }

        // exhaustPoints가 비어있으면 자기 자신 위치 사용
        if (exhaustPoints == null || exhaustPoints.Length == 0)
            exhaustPoints = new Transform[] { transform };

        _psList    = new ParticleSystem[exhaustPoints.Length];
        _emissions = new ParticleSystem.EmissionModule[exhaustPoints.Length];
        _mains     = new ParticleSystem.MainModule[exhaustPoints.Length];

        for (int i = 0; i < exhaustPoints.Length; i++)
        {
            Transform spawnAt = exhaustPoints[i] != null ? exhaustPoints[i] : transform;
            var go = PoolManager.Instance.Spawn(exhaustPrefab, spawnAt.position, spawnAt.rotation);
            go.transform.SetParent(spawnAt, worldPositionStays: true);

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                Debug.LogWarning($"[TankExhaustEffects] exhaust 프리팹에 ParticleSystem이 없습니다. (index {i})");
                continue;
            }

            if (!ps.isPlaying) ps.Play(withChildren: true);

            _psList[i]    = ps;
            _emissions[i] = ps.emission;
            _mains[i]     = ps.main;
        }

        // 초기값
        _curColor    = idleColor;
        _curEmission = idleEmissionRate;
        _curSize     = idleStartSize;
        _curSpeed    = idleStartSpeed;

        if (moduleManager != null)
            moduleManager.OnTankDestroyed += OnTankDestroyed;
    }

    private void OnDestroy()
    {
        if (moduleManager != null)
            moduleManager.OnTankDestroyed -= OnTankDestroyed;

        // OnTankDestroyed에서 이미 ReturnDelayed 처리한 경우 중복 반환 방지
        if (_poolReturned || _psList == null) return;

        for (int i = 0; i < _psList.Length; i++)
        {
            if (_psList[i] == null) continue;
            var go = _psList[i].gameObject;
            if (go == null) continue;
            go.transform.SetParent(null);
            PoolManager.Instance?.Return(go);
        }
    }

    private void Update()
    {
        if (_dead || _psList == null) return;

        UpdateState();
        ApplyToParticle();
    }

    // ── 상태 판정 ────────────────────────────────
    private void UpdateState()
    {
        if (IsEngineDamaged())
        {
            _state = ExhaustState.Damaged;
            return;
        }

        float throttle = driverController != null
            ? Mathf.Abs(driverController.CurrentThrottle)
            : 0f;

        _state = throttle >= throttleThreshold
            ? ExhaustState.Throttle
            : ExhaustState.Idle;
    }

    private bool IsEngineDamaged()
    {
        if (moduleManager == null) return false;

        var modules = moduleManager.GetAliveInternalModules();
        for (int i = 0; i < modules.Count; i++)
        {
            var m = modules[i];
            if (m.Type == ModuleType.Engine && m.State == ModuleState.Damaged)
                return true;
        }
        return false;
    }

    // ── 파티클 보간 적용 ──────────────────────────
    private void ApplyToParticle()
    {
        float dt = Time.deltaTime;

        float targetEmission;
        Color targetColor;
        float targetSize;
        float targetSpeed;

        switch (_state)
        {
            case ExhaustState.Throttle:
                targetEmission = throttleEmissionRate;
                targetColor    = throttleColor;
                targetSize     = throttleStartSize;
                targetSpeed    = throttleStartSpeed;
                break;
            case ExhaustState.Damaged:
                targetEmission = damagedEmissionRate;
                targetColor    = damagedColor;
                targetSize     = damagedStartSize;
                targetSpeed    = damagedStartSpeed;
                break;
            default:
                targetEmission = idleEmissionRate;
                targetColor    = idleColor;
                targetSize     = idleStartSize;
                targetSpeed    = idleStartSpeed;
                break;
        }

        _curEmission = Mathf.Lerp(_curEmission, targetEmission, emissionLerpSpeed * dt);
        _curColor    = Color.Lerp(_curColor,    targetColor,    colorLerpSpeed    * dt);
        _curSize     = Mathf.Lerp(_curSize,     targetSize,     sizeLerpSpeed     * dt);
        _curSpeed    = Mathf.Lerp(_curSpeed,    targetSpeed,    sizeLerpSpeed     * dt);

        for (int i = 0; i < _psList.Length; i++)
        {
            if (_psList[i] == null) continue;
            _emissions[i].rateOverTime = _curEmission;
            _mains[i].startColor       = _curColor;
            _mains[i].startSize        = _curSize;
            _mains[i].startSpeed       = _curSpeed;
        }
    }

    // ── 격파 시 정지 + 풀 반환 ───────────────────
    private void OnTankDestroyed()
    {
        _dead = true;
        if (_psList == null) return;

        for (int i = 0; i < _psList.Length; i++)
        {
            if (_psList[i] == null) continue;

            _emissions[i].rateOverTime = 0f;
            _psList[i].Stop(withChildren: true,
                stopBehavior: ParticleSystemStopBehavior.StopEmitting);

            // 기존 파티클이 자연 소멸한 뒤 풀에 반환
            float maxLife = _psList[i].main.startLifetime.constantMax;
            var go = _psList[i].gameObject;
            go.transform.SetParent(null);
            PoolManager.Instance.ReturnDelayed(go, Mathf.Max(maxLife, 1f) + 0.3f);
        }

        _poolReturned = true;
        _psList       = null;
    }
}
