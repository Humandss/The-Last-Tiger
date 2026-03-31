using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 플레이어 탱크가 격파될 때 브리핑 씬으로 리셋합니다.
/// 플레이어 탱크 루트에 부착하세요.
/// </summary>
public class PlayerDeathHandler : MonoBehaviour
{
    [Header("씬 전환")]
    [SerializeField] private string briefingSceneName = "BriefingScene";
    [SerializeField] private float  fadeDelay         = 1.5f;
    [SerializeField] private float  fadeDuration      = 1.2f;

    private ModuleManager _moduleManager;
    private bool          _triggered;

    private void Awake()
    {
        // 같은 오브젝트 또는 부모에서도 탐색
        _moduleManager = GetComponent<ModuleManager>();
        if (_moduleManager == null)
            _moduleManager = GetComponentInParent<ModuleManager>();

        if (_moduleManager == null)
            Debug.LogError("[PlayerDeathHandler] ModuleManager를 찾을 수 없습니다! 같은 오브젝트에 있는지 확인하세요.");
        else
            Debug.Log($"[PlayerDeathHandler] ModuleManager 연결 완료: {_moduleManager.gameObject.name}");
    }

    private void OnEnable()
    {
        if (_moduleManager != null)
        {
            _moduleManager.OnTankDestroyed += HandlePlayerDeath;
            Debug.Log("[PlayerDeathHandler] OnTankDestroyed 구독 완료");
        }
    }

    private void OnDisable()
    {
        if (_moduleManager != null)
            _moduleManager.OnTankDestroyed -= HandlePlayerDeath;
    }

    private void HandlePlayerDeath()
    {
        Debug.Log("[PlayerDeathHandler] HandlePlayerDeath 호출됨!");

        if (_triggered) return;
        _triggered = true;

        // 사망 즉시 캔버스 생성 → GUI 즉시 차단
        var canvasGo = BuildFadeCanvas(out var cg);
        StartCoroutine(DeathSequence(canvasGo, cg));
    }

    private GameObject BuildFadeCanvas(out CanvasGroup cg)
    {
        var canvasGo = new GameObject("DeathFadeCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasGo);

        var canvas          = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767; // Unity 최대값 → 무조건 최상단

        var scaler                 = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        cg                = canvasGo.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.blocksRaycasts = true; // 입력도 차단

        var bg     = new GameObject("Black", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = Color.black;

        return canvasGo;
    }

    private IEnumerator DeathSequence(GameObject canvasGo, CanvasGroup cg)
    {
        Debug.Log($"[PlayerDeathHandler] {fadeDelay}초 후 페이드 시작");

        // 폭발/사망 연출 감상 시간 (캔버스는 이미 존재하지만 투명)
        yield return new WaitForSeconds(fadeDelay);

        // 페이드인
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        cg.alpha = 1f;

        // 씬 로드 후 캔버스가 자동으로 페이드아웃되도록 컴포넌트 추가
        var fadeOut = canvasGo.AddComponent<DeathCanvasFadeOut>();
        fadeOut.Init(cg, fadeDuration);

        Debug.Log($"[PlayerDeathHandler] LoadScene: {briefingSceneName}");
        SceneManager.LoadScene(briefingSceneName);
    }
}

/// <summary>
/// 씬 전환 후 검정 캔버스를 자동으로 페이드아웃하고 제거합니다.
/// </summary>
public class DeathCanvasFadeOut : MonoBehaviour
{
    private CanvasGroup _cg;
    private float       _duration;

    public void Init(CanvasGroup cg, float duration)
    {
        _cg       = cg;
        _duration = duration;
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        // 브리핑 UI가 초기화될 시간을 약간 기다림
        yield return new WaitForSecondsRealtime(0.1f);

        float t = 0f;
        while (t < _duration)
        {
            t      += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Lerp(1f, 0f, t / _duration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
