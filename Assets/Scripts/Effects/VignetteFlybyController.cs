using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 포탄이 카메라 가까이 스쳐 지나갈 때 Vignette 강도를 일시적으로 높여
/// 터널 비전(시야 좁아지는 듯한 긴장감) 효과 연출.
///
/// - 여러 포탄이 동시에 가까이 있을 수록 누적되어 더 강해짐
/// - 시간 지나면 자연 감쇠
///
/// 사용법:
/// - 씬에 빈 GameObject 만들고 이 스크립트 부착
/// - Volume 슬롯에 Bloom/Vignette 들어있는 Global Volume 드래그
/// - BallisticManager가 자동으로 Boost() 호출
/// </summary>
public class VignetteFlybyController : MonoBehaviour
{
    public static VignetteFlybyController Instance { get; private set; }

    [Header("Volume")]
    [Tooltip("Vignette 컴포넌트가 들어있는 Global Volume")]
    [SerializeField] private Volume targetVolume;

    [Header("Boost Settings")]
    [Tooltip("최대 가산 강도 (base intensity 위에 더해짐)")]
    [SerializeField] private float maxBoost = 0.4f;
    [Tooltip("초당 감쇠량 — 클수록 빨리 사라짐 (낮을수록 여운 길게)")]
    [SerializeField] private float decayRate = 0.6f;
    [Tooltip("최종 intensity 상한 (1.0 = 화면 거의 검정)")]
    [SerializeField] private float intensityCeiling = 0.85f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private Vignette vignette;
    private float baseIntensity;
    private float currentBoost;
    private bool ready;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (targetVolume == null)
        {
            Debug.LogWarning("[VignetteFlyby] Volume 미할당 — Inspector에서 Volume 드래그 필요");
            return;
        }

        // Profile 인스턴스화 — 원본 Asset 영구 변경 방지
        targetVolume.profile = Instantiate(targetVolume.profile);

        if (targetVolume.profile.TryGet(out vignette))
        {
            // 컴포넌트 활성화 + Override state 강제 ON
            vignette.active = true;
            vignette.intensity.overrideState = true;

            baseIntensity = vignette.intensity.value;
            ready = true;

            Debug.Log($"[VignetteFlyby] Ready. Base Intensity = {baseIntensity:F3}");
        }
        else
        {
            Debug.LogWarning("[VignetteFlyby] Volume Profile에 Vignette 컴포넌트 없음 — Add Override → Post-processing → Vignette 필요");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!ready) return;

        // 시간 따라 부스트 감쇠
        currentBoost = Mathf.Max(0f, currentBoost - decayRate * Time.deltaTime);

        float finalIntensity = Mathf.Min(intensityCeiling, baseIntensity + currentBoost);
        // Override() = value + overrideState 같이 설정 (확실하게 적용)
        vignette.intensity.Override(finalIntensity);
    }

    /// <summary>
    /// 외부에서 부스트 추가 (BallisticManager 등에서 호출).
    /// 여러 호출이 누적되어 더 강해짐.
    /// </summary>
    public void Boost(float amount)
    {
        if (!ready) return;
        currentBoost = Mathf.Min(maxBoost, currentBoost + amount);

        if (debugLog) Debug.Log($"[VignetteFlyby] Boost +{amount:F3} → currentBoost={currentBoost:F3}");
    }

    public float CurrentBoost => currentBoost;
    public float CurrentIntensity => baseIntensity + currentBoost;
}
