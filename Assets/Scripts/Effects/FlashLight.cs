using UnityEngine;

/// <summary>
/// 짧은 순간 번쩍이고 페이드되는 라이트 효과.
/// 머즐 플래시, 착탄 폭발, 탄약 유폭 등에 부착하여 자동으로 빛이 사라짐.
///
/// 사용법:
/// - 빈 GameObject에 Light 컴포넌트 + 이 스크립트 부착
/// - 또는 기존 이펙트 프리팹의 자식으로 Light + FlashLight 추가
/// - PoolManager.Spawn 시 OnEnable에서 자동 시작 → duration 후 자동 페이드아웃
/// </summary>
public class FlashLight : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("페이드시킬 Light 컴포넌트. 비워두면 같은 GameObject에서 자동 검색.")]
    [SerializeField] private Light targetLight;

    [Header("Flash Settings")]
    [SerializeField] private float maxIntensity = 10f;
    [SerializeField] private Color flashColor = new Color(1f, 0.75f, 0.4f);
    [SerializeField] private float duration = 0.3f;
    [Tooltip("시간(0~1) 따른 강도 배율 곡선. 기본은 빠르게 켜졌다 부드럽게 사라짐.")]
    [SerializeField] private AnimationCurve falloffCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(0.15f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 0f)
    );

    [Header("Behavior")]
    [Tooltip("재생 끝나면 GameObject 자체를 비활성화 (PoolManager 회수용)")]
    [SerializeField] private bool disableOnFinish = false;

    private float elapsed;
    private bool isPlaying;

    private void Reset()
    {
        targetLight = GetComponent<Light>();
        if (targetLight != null)
        {
            targetLight.color = flashColor;
            targetLight.intensity = maxIntensity;
        }
    }

    private void OnEnable()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();
        if (targetLight == null)
        {
            Debug.LogWarning($"[FlashLight] {name}: Light 컴포넌트 없음.");
            enabled = false;
            return;
        }

        elapsed = 0f;
        isPlaying = true;
        targetLight.color = flashColor;
        targetLight.intensity = maxIntensity;
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

            if (disableOnFinish) gameObject.SetActive(false);
            return;
        }

        float curveValue = falloffCurve.Evaluate(t);
        targetLight.intensity = maxIntensity * curveValue;
    }
}
