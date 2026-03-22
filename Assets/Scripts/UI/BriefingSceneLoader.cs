using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 브리핑 씬에서 게임 씬으로 전환합니다.
/// MissionBriefingUI의 OnBriefingComplete 이벤트에 연결하세요.
/// </summary>
public class BriefingSceneLoader : MonoBehaviour
{
    [Header("전환 설정")]
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private float  blackFadeDuration = 0.6f;

    // ── 암전 오버레이 ─────────────────────────────────────
    private CanvasGroup _blackCg;

    private void Awake()
    {
        BuildBlackOverlay();
    }

    private void Start()
    {
        // 인스펙터 연결 없이도 자동으로 이벤트 구독
        var briefing = GetComponent<MissionBriefingUI>();
        if (briefing != null)
            briefing.OnBriefingComplete.AddListener(LoadGameScene);
        else
            Debug.LogWarning("[BriefingSceneLoader] MissionBriefingUI를 같은 오브젝트에서 찾지 못했습니다.");
    }

    // OnBriefingComplete 에 연결할 메서드 (인스펙터에서 수동 연결도 가능)
    public void LoadGameScene()
    {
        StartCoroutine(FadeAndLoad());
    }

    private IEnumerator FadeAndLoad()
    {
        // 암전
        float t = 0f;
        while (t < blackFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _blackCg.alpha = Mathf.Clamp01(t / blackFadeDuration);
            yield return null;
        }
        _blackCg.alpha = 1f;

        SceneManager.LoadScene(gameSceneName);
    }

    // ── 전체 화면 검정 패널 생성 ──────────────────────────
    private void BuildBlackOverlay()
    {
        var canvasGo = new GameObject("BlackOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas          = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;   // BriefingUI(30000)보다 위

        var scaler                 = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _blackCg       = canvasGo.AddComponent<CanvasGroup>();
        _blackCg.alpha = 0f;
        _blackCg.blocksRaycasts = false;

        var imgGo   = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(canvasGo.transform, false);
        var imgRect = imgGo.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;
        imgGo.GetComponent<Image>().color = Color.black;
    }
}
