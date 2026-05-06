using UnityEngine;

/// <summary>
/// 피격 후 뜨거운 금속이 천천히 식는 효과를 라이트로 표현.
/// 시간 따라 색상은 흰빛/노랑 → 주황 → 빨강 → 어두운 빨강으로,
/// 강도는 처음 강하게 시작해 부드럽게 사그라듦.
///
/// 사용법:
/// - 데칼 프리팹의 자식으로 빈 GameObject + Light + 이 스크립트
/// - 데칼이 PoolManager로 spawn 되면 OnEnable 자동 시작
/// - duration 끝나면 라이트 OFF, 데칼은 BudgetManager가 회수
/// </summary>
public class HeatGlowLight : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("제어할 Light. 비워두면 같은 GameObject에서 자동 검색")]
    [SerializeField] private Light targetLight;

    [Header("Glow Settings")]
    [Tooltip("최대 강도 (HDR이면 5~30 권장)")]
    [SerializeField] private float startIntensity = 8f;
    [Tooltip("완전히 식기까지 걸리는 시간(초)")]
    [SerializeField] private float duration = 2.5f;

    [Header("Color over Lifetime")]
    [Tooltip("시간(0=시작, 1=끝)에 따른 색상. 흰빛 → 주황 → 빨강 → 어두운 빨강")]
    [SerializeField]
    private Gradient colorOverTime = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(new Color(1.00f, 0.95f, 0.75f), 0.00f),  // 흰-노랑 (가장 뜨거움)
            new GradientColorKey(new Color(1.00f, 0.65f, 0.20f), 0.20f),  // 주황
            new GradientColorKey(new Color(1.00f, 0.30f, 0.05f), 0.55f),  // 주황-빨강
            new GradientColorKey(new Color(0.50f, 0.05f, 0.00f), 1.00f),  // 어두운 빨강
        },
        alphaKeys = new[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f),
        },
    };

    [Header("Intensity over Lifetime")]
    [Tooltip("시간(0=시작, 1=끝)에 따른 강도 배율. 처음 강하게, 점점 사그라듦")]
    [SerializeField]
    private AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0.00f, 1.00f),
        new Keyframe(0.20f, 0.80f),
        new Keyframe(0.50f, 0.40f),
        new Keyframe(0.80f, 0.10f),
        new Keyframe(1.00f, 0.00f)
    );

    private float elapsed;
    private bool isPlaying;

    private void Reset()
    {
        targetLight = GetComponent<Light>();
    }

    private void OnEnable()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();
        if (targetLight == null)
        {
            Debug.LogWarning($"[HeatGlowLight] {name}: Light 컴포넌트 없음.");
            enabled = false;
            return;
        }

        elapsed = 0f;
        isPlaying = true;

        targetLight.color = colorOverTime.Evaluate(0f);
        targetLight.intensity = startIntensity * intensityCurve.Evaluate(0f);
        targetLight.enabled = true;
    }

    private void OnDisable()
    {
        isPlaying = false;
        if (targetLight != null)
        {
            targetLight.intensity = 0f;
            targetLight.enabled = false;
        }
    }

    private void Update()
    {
        if (!isPlaying || targetLight == null) return;

        elapsed += Time.deltaTime;
        float t = duration > 0f ? elapsed / duration : 1f;

        if (t >= 1f)
        {
            targetLight.intensity = 0f;
            targetLight.enabled = false;
            isPlaying = false;
            return;
        }

        targetLight.color = colorOverTime.Evaluate(t);
        targetLight.intensity = startIntensity * intensityCurve.Evaluate(t);
    }
}
