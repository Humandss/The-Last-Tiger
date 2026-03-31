using UnityEngine;

/// <summary>
/// 파티클 이펙트에 관성 효과를 줍니다.
/// 루트 오브젝트(탱크)가 이동할 때 연기/불꽃이 진행 방향 반대로 흘러가는 효과.
///
/// 사용법: 화재/연기 VFX 프리팹 루트에 부착하면 됩니다.
/// 스폰 후 부모 계층에서 Rigidbody를 자동으로 탐색합니다.
/// </summary>
public class ParticleInertia : MonoBehaviour
{
    [Header("관성 설정")]
    [Tooltip("속도 반영 강도. 클수록 연기가 더 많이 뒤로 흘림.")]
    [SerializeField] private float inertiaStrength = 0.6f;

    [Tooltip("적용 가능한 최대 힘 (m/s²). 너무 크면 연기가 과도하게 날아감.")]
    [SerializeField] private float maxForce = 12f;

    [Tooltip("속도 스무딩. 클수록 관성 반응이 빠름.")]
    [SerializeField] private float smoothing = 4f;

    [Tooltip("Y축(수직) 힘 억제 비율. 1 = 수직 영향 없음, 0 = 수직도 동일하게 적용.")]
    [SerializeField, Range(0f, 1f)] private float verticalDamp = 0.3f;

    // ──────────────────────────────────────────────
    private ParticleSystem[]  _particles;
    private Rigidbody         _rb;
    private Transform         _anchor;

    private Vector3 _prevAnchorPos;
    private Vector3 _smoothedVelocity;
    private bool    _ready;

    // ──────────────────────────────────────────────
    private void OnEnable()
    {
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _smoothedVelocity = Vector3.zero;
        _ready = false;

        // 활성화 시점에 부모가 없을 수도 있으므로 다음 프레임에 탐색
    }

    private void Update()
    {
        // 첫 프레임: 부모 계층에서 Rigidbody 탐색
        if (!_ready)
        {
            FindAnchor();
            _ready = true;
        }

        if (_anchor == null) return;

        // 속도 계산 (Rigidbody 있으면 우선 사용, 없으면 위치 차분)
        Vector3 rawVelocity;
        if (_rb != null)
        {
            rawVelocity = _rb.velocity;
        }
        else
        {
            rawVelocity = (_anchor.position - _prevAnchorPos) / Mathf.Max(Time.deltaTime, 1e-4f);
            _prevAnchorPos = _anchor.position;
        }

        // 스무딩
        _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, rawVelocity, smoothing * Time.deltaTime);

        // 관성 = 속도의 반대 방향 (연기가 뒤로 흐르는 효과)
        Vector3 force = -_smoothedVelocity * inertiaStrength;
        force.y *= (1f - verticalDamp); // 수직축 억제
        force = Vector3.ClampMagnitude(force, maxForce);

        // 파티클 시스템 전체에 forceOverLifetime 적용
        ApplyForce(force);
    }

    private void FindAnchor()
    {
        // 부모 계층을 따라 올라가며 Rigidbody 탐색
        Transform cur = transform.parent;
        while (cur != null)
        {
            var rb = cur.GetComponent<Rigidbody>();
            if (rb != null)
            {
                _rb     = rb;
                _anchor = cur;
                _prevAnchorPos = cur.position;
                return;
            }
            cur = cur.parent;
        }

        // Rigidbody 없으면 부모 트랜스폼만 사용
        if (transform.parent != null)
        {
            _anchor = transform.parent;
            _prevAnchorPos = _anchor.position;
        }
    }

    private void ApplyForce(Vector3 force)
    {
        foreach (var ps in _particles)
        {
            if (ps == null) continue;

            var fol   = ps.forceOverLifetime;
            fol.enabled = true;
            fol.space   = ParticleSystemSimulationSpace.World;

            fol.x = new ParticleSystem.MinMaxCurve(force.x);
            fol.y = new ParticleSystem.MinMaxCurve(force.y);
            fol.z = new ParticleSystem.MinMaxCurve(force.z);
        }
    }

    // 풀에 반환될 때 힘 초기화
    private void OnDisable()
    {
        _anchor = null;
        _rb     = null;
        _ready  = false;
        _smoothedVelocity = Vector3.zero;

        if (_particles == null) return;
        foreach (var ps in _particles)
        {
            if (ps == null) continue;
            var fol     = ps.forceOverLifetime;
            fol.x       = new ParticleSystem.MinMaxCurve(0f);
            fol.y       = new ParticleSystem.MinMaxCurve(0f);
            fol.z       = new ParticleSystem.MinMaxCurve(0f);
        }
    }
}
