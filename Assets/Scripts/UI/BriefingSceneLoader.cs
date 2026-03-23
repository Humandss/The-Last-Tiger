using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 브리핑 씬에서 게임 씬으로 전환합니다.
/// 전환 순서:
///   1. 화면 암전
///   2. 좌하단에 작전 정보 카드 순차 등장
///   3. 잠시 대기
///   4. 씬 로드
/// </summary>
public class BriefingSceneLoader : MonoBehaviour
{
    [Header("전환 설정")]
    [SerializeField] private string gameSceneName     = "SampleScene";
    [SerializeField] private float  blackFadeDuration = 0.6f;
    [SerializeField] private float  cardLineInterval    = 0.15f; // 줄 사이 등장 간격
    [SerializeField] private float  cardHoldDuration   = 2.8f;  // 카드 전체 표시 후 대기
    [SerializeField] private float  cardFadeOutDuration = 0.8f; // 카드 페이드아웃 시간
    [SerializeField] private bool   skipOnAnyKey       = true;  // 카드 표시 중 아무 키로 스킵

    [Header("카드 스타일")]
    [SerializeField] private int   cardFontSize  = 22;
    [SerializeField] private float cardPadLeft   = 72f;
    [SerializeField] private float cardPadBottom = 80f;
    [SerializeField] private float cardLineHeight = 34f;

    [Header("로딩 표시")]
    [SerializeField] private string loadingLabel       = "로딩 중";
    [SerializeField] private float  loadingDotsInterval = 0.4f;  // 점 하나 추가 간격
    [SerializeField] private int    loadingFontSize     = 18;

    // 브리핑 데이터는 같은 오브젝트의 MissionBriefingUI에서 자동으로 가져옴
    private MissionBriefingData _data;
    private CanvasGroup _blackCg;
    private GameObject  _overlayCanvas;
    private readonly List<CanvasGroup> _cardLines = new List<CanvasGroup>();
    private Text _loadingText;
    private bool _skipped = false;

    // SampleScene의 SceneFadeIn이 이 이름으로 검색함
    public const string OverlayName = "TransitionOverlay";

    private void Awake()
    {
        var briefingUI = GetComponent<MissionBriefingUI>();
        if (briefingUI != null)
            _data = briefingUI.Data;

        BuildOverlay();
    }

    private void Start()
    {
        var briefing = GetComponent<MissionBriefingUI>();
        if (briefing != null)
            briefing.OnBriefingComplete.AddListener(LoadGameScene);
        else
            Debug.LogWarning("[BriefingSceneLoader] MissionBriefingUI를 같은 오브젝트에서 찾지 못했습니다.");
    }

    public void LoadGameScene() => StartCoroutine(TransitionRoutine());

    private void Update()
    {
        if (skipOnAnyKey && !_skipped && Input.anyKeyDown)
            _skipped = true;
    }

    // ── 전환 시퀀스 ──────────────────────────────────────
    private IEnumerator TransitionRoutine()
    {
        _skipped = false;

        // 1. 암전
        yield return FadeBlack(blackFadeDuration);

        // 스킵: 카드 없이 바로 전환
        if (_skipped)
        {
            foreach (var cg in _cardLines) cg.alpha = 0f;
            goto load;
        }

        // 2. 카드 줄 순차 등장 — 스킵 시 즉시 전체 표시
        for (int i = 0; i < _cardLines.Count; i++)
        {
            if (_skipped)
            {
                for (int j = i; j < _cardLines.Count; j++)
                    _cardLines[j].alpha = 1f;
                break;
            }
            _cardLines[i].alpha = 1f;
            yield return new WaitForSecondsRealtime(cardLineInterval);
        }

        // 3. 대기 — 스킵 시 생략 (로딩 도트 애니메이션)
        if (!_skipped)
        {
            var dotsRoutine = StartCoroutine(LoadingDotsRoutine());
            yield return new WaitForSecondsRealtime(cardHoldDuration);
            StopCoroutine(dotsRoutine);
        }

        // 4. 카드 전체 동시 페이드아웃
        float t = 0f;
        while (t < cardFadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / cardFadeOutDuration);
            foreach (var cg in _cardLines) cg.alpha = a;
            yield return null;
        }
        foreach (var cg in _cardLines) cg.alpha = 0f;

        load:

        // 5. 오버레이를 씬 전환 이후까지 유지 → SceneFadeIn이 페이드아웃
        if (_overlayCanvas != null)
        {
            _overlayCanvas.transform.SetParent(null);   // 부모에서 분리 (DontDestroyOnLoad 요건)
            DontDestroyOnLoad(_overlayCanvas);
        }

        // 6. 씬 로드
        SceneManager.LoadScene(gameSceneName);
    }

    // ── 오버레이 빌드 ────────────────────────────────────
    private void BuildOverlay()
    {
        var canvasGo = new GameObject(OverlayName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _overlayCanvas = canvasGo;

        var canvas          = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;

        var scaler                 = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _blackCg               = canvasGo.AddComponent<CanvasGroup>();
        _blackCg.alpha         = 0f;
        _blackCg.blocksRaycasts = false;

        // 검정 패널
        var bg     = new GameObject("Black", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = Color.black;

        if (_data == null) return;

        // 작전명에서 "작전명 : " 접두사 제거
        string missionShort = _data.missionName ?? "";
        int idx = missionShort.IndexOf(':');
        if (idx >= 0 && idx < missionShort.Length - 1)
            missionShort = missionShort.Substring(idx + 1).Trim();

        // 카드 줄 정의 (key, val, valColor override — null이면 기본색)
        Color missionGreen = new Color(0.35f, 0.90f, 0.45f, 1f);
        var lines = new (string key, string val, Color? valOverride)[]
        {
            ("작전명", missionShort,               missionGreen),
            ("위치",   _data.transitionLocation,   null),
            ("시각",   _data.transitionDate,        null),
            ("전차장", _data.transitionCommander,  null),
        };

        Color keyColor = new Color(0.60f, 0.60f, 0.60f, 1f);
        Color valColor = new Color(0.88f, 0.88f, 0.80f, 1f);
        Font  font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        for (int i = 0; i < lines.Length; i++)
        {
            float yPos = cardPadBottom + (lines.Length - 1 - i) * cardLineHeight;

            // 줄 컨테이너
            var rowGo   = new GameObject($"Row_{lines[i].key}", typeof(RectTransform), typeof(CanvasGroup));
            rowGo.transform.SetParent(canvasGo.transform, false);

            var rowCg      = rowGo.GetComponent<CanvasGroup>();
            rowCg.alpha    = 0f;
            _cardLines.Add(rowCg);

            var rowRect       = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 0f);
            rowRect.anchorMax = new Vector2(0f, 0f);
            rowRect.pivot     = new Vector2(0f, 0f);
            rowRect.sizeDelta = new Vector2(700f, cardLineHeight);
            rowRect.anchoredPosition = new Vector2(cardPadLeft, yPos);

            Color thisValColor = lines[i].valOverride ?? valColor;

            // 키 텍스트
            MakeText(rowGo.transform, lines[i].key, keyColor,     cardFontSize, font, 0f,   130f);
            // 구분선
            MakeText(rowGo.transform, "—",          keyColor,     cardFontSize, font, 130f, 30f);
            MakeText(rowGo.transform, lines[i].val, thisValColor, cardFontSize, font, 168f, 500f);
        }

        // 로딩 표시 (우하단) — 카드와 함께 페이드아웃
        var loadGo   = new GameObject("LoadingIndicator", typeof(RectTransform), typeof(CanvasGroup));
        loadGo.transform.SetParent(canvasGo.transform, false);
        var loadCg   = loadGo.GetComponent<CanvasGroup>();
        loadCg.alpha = 0f;
        _cardLines.Add(loadCg);

        var loadRect          = loadGo.GetComponent<RectTransform>();
        loadRect.anchorMin    = new Vector2(1f, 0f);
        loadRect.anchorMax    = new Vector2(1f, 0f);
        loadRect.pivot        = new Vector2(1f, 0f);
        loadRect.sizeDelta    = new Vector2(300f, cardLineHeight);
        loadRect.anchoredPosition = new Vector2(-cardPadLeft, cardPadBottom);

        Color loadColor = new Color(0.50f, 0.50f, 0.50f, 1f);
        _loadingText = MakeTextReturn(loadGo.transform, loadingLabel + " .", loadColor, loadingFontSize, font);
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    private IEnumerator LoadingDotsRoutine()
    {
        string[] states = { " .", " ..", " ..." };
        int idx = 0;
        while (true)
        {
            if (_loadingText != null)
                _loadingText.text = loadingLabel + states[idx % states.Length];
            idx++;
            yield return new WaitForSecondsRealtime(loadingDotsInterval);
        }
    }

    // 텍스트를 반환해야 할 때 사용하는 오버로드
    private static Text MakeTextReturn(Transform parent, string text, Color color, int fontSize, Font font)
    {
        var go   = new GameObject("T", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect       = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var t                = go.GetComponent<Text>();
        t.text               = text ?? "";
        t.color              = color;
        t.fontSize           = fontSize;
        t.font               = font;
        t.alignment          = TextAnchor.MiddleRight;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.raycastTarget      = false;
        return t;
    }

    private static void MakeText(Transform parent, string text, Color color, int fontSize,
                                  Font font, float xOffset, float width)
    {
        var go   = new GameObject("T", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect       = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot     = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(xOffset, 0f);
        rect.offsetMax = new Vector2(xOffset + width, 0f);

        var t                = go.GetComponent<Text>();
        t.text               = text ?? "";
        t.color              = color;
        t.fontSize           = fontSize;
        t.font               = font;
        t.alignment          = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.raycastTarget      = false;
    }

    private IEnumerator FadeBlack(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _blackCg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        _blackCg.alpha = 1f;
    }

}
