using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 씬 시작 시 작전 지령을 브리핑 씬과 동일한 스타일로 표시합니다.
/// 확인 버튼을 누르면 일시 정지가 해제됩니다.
/// </summary>
public class MissionOrderUI : MonoBehaviour
{
    // ── 데이터 ───────────────────────────────────────────
    [Header("데이터")]
    [SerializeField] private MissionBriefingData data;

    // ── 타이밍 ──────────────────────────────────────────
    [Header("타이밍 (초)")]
    [SerializeField] private float showDelay        = 1.1f;
    [SerializeField] private float overlayFadeIn    = 0.7f;
    [SerializeField] private float headerStagger    = 0.45f;
    [SerializeField] private float bodyFadeIn       = 0.35f;
    [SerializeField] private float bodyLinePause    = 0.3f;
    [SerializeField] private float buttonFadeIn     = 0.5f;
    [SerializeField] private float typewriterSpeed  = 0.04f;  // 글자 당 간격(초)

    // ── 스타일 ──────────────────────────────────────────
    [Header("스타일")]
    [SerializeField] private Font  customFont;
    [SerializeField] private Color bgColor        = new Color(0.04f, 0.04f, 0.06f, 0.93f);
    [SerializeField] private Color accentColor    = new Color(0.85f, 0.72f, 0.20f, 1f);
    [SerializeField] private Color headerDimColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color bodyColor      = new Color(0.88f, 0.88f, 0.88f, 1f);
    [SerializeField] private int   baseFontSize   = 28;
    [SerializeField] [Range(0f, 0.4f)] private float scanlineAlpha = 0.12f;

    // ── 내부 상태 ────────────────────────────────────────
    private Font          _font;
    private CanvasGroup   _rootCg;
    private RectTransform _panelRect;
    private float         _nextY     = -10f;
    private bool          _confirmed = false;

    private readonly List<(CanvasGroup cg, Text text)> _allLabels
        = new List<(CanvasGroup, Text)>();

    // ─────────────────────────────────────────────────────
    private void Awake()
    {
        _font = customFont != null
            ? customFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;
        if (ModuleDebugHUD.Instance != null) ModuleDebugHUD.Instance.show = false;
        BuildCanvas();
        _rootCg.alpha = 0f;
    }

    public event Action OnConfirmed;

    /// <summary>런타임에 데이터를 주입합니다. Start() 호출 이전에 사용하세요.</summary>
    public void SetData(MissionBriefingData d) => data = d;

    private void Start()
    {
        StartCoroutine(ShowSequence());
    }

    private void Update()
    {
        if (_confirmed) return;
        if (_rootCg == null || _rootCg.alpha < 0.5f) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            Confirm();
    }

    private void Confirm()
    {
        if (_confirmed) return;
        _confirmed = true;
        StartCoroutine(HideAndResume());
    }

    // ── 연출 시퀀스 ──────────────────────────────────────
    private IEnumerator ShowSequence()
    {
        yield return new WaitForSecondsRealtime(showDelay);

        yield return FadeCg(_rootCg, 0f, 1f, overlayFadeIn);

        yield return ShowHeader();
        yield return new WaitForSecondsRealtime(0.3f);

        yield return ShowBody();
        yield return new WaitForSecondsRealtime(0.2f);

        yield return ShowButton();
    }

    private IEnumerator HideAndResume()
    {
        yield return FadeCg(_rootCg, 1f, 0f, 0.4f);
        if (ModuleDebugHUD.Instance != null) ModuleDebugHUD.Instance.show = true;
        Time.timeScale = 1f;
        OnConfirmed?.Invoke();
        Destroy(gameObject);
    }

    // ── 헤더 ─────────────────────────────────────────────
    private IEnumerator ShowHeader()
    {
        if (data == null) yield break;

        if (data.logoSprite != null)
        {
            float size  = data.logoSize;
            var logoGo  = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            logoGo.transform.SetParent(_panelRect, false);
            var logoCg            = logoGo.GetComponent<CanvasGroup>();
            logoCg.alpha          = 0f;
            logoCg.interactable   = false;
            logoCg.blocksRaycasts = false;
            var logoRect              = logoGo.GetComponent<RectTransform>();
            logoRect.anchorMin        = new Vector2(0.5f, 1f);
            logoRect.anchorMax        = new Vector2(0.5f, 1f);
            logoRect.pivot            = new Vector2(0.5f, 1f);
            logoRect.sizeDelta        = new Vector2(size, size);
            logoRect.anchoredPosition = new Vector2(0f, _nextY);
            var logoImg            = logoGo.GetComponent<Image>();
            logoImg.sprite         = data.logoSprite;
            logoImg.preserveAspect = true;
            logoImg.raycastTarget  = false;
            logoImg.material = new Material(Shader.Find("UI/Default"));
            logoImg.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            logoImg.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            _nextY -= size + 14f;
            yield return FadeInCg(logoCg, headerStagger);
            yield return new WaitForSecondsRealtime(headerStagger * 0.3f);
        }

        var cgClass = AddLabel(data.classification, headerDimColor,
                               baseFontSize - 4, TextAnchor.UpperCenter);
        yield return FadeInCg(cgClass, headerStagger * 0.5f);
        yield return new WaitForSecondsRealtime(headerStagger * 0.5f);

        _nextY -= 4f;
        AddDivider(accentColor, 700f);
        yield return new WaitForSecondsRealtime(0.15f);

        string missionShort = data.missionName ?? "";
        var cgMission = AddLabel(missionShort, accentColor,
                                 baseFontSize + 6, TextAnchor.UpperCenter, bold: true);
        yield return FadeInCg(cgMission, headerStagger);
        yield return new WaitForSecondsRealtime(headerStagger * 0.4f);

        var cgDate = AddLabel(data.dateLocation, headerDimColor,
                              baseFontSize - 3, TextAnchor.UpperCenter);
        yield return FadeInCg(cgDate, headerStagger * 0.6f);
        yield return new WaitForSecondsRealtime(0.2f);

        _nextY -= 4f;
        AddDivider(new Color(0.35f, 0.35f, 0.35f, 1f), 700f);
    }

    // ── 본문 ─────────────────────────────────────────────
    private IEnumerator ShowBody()
    {
        string orderBody = data != null ? data.orderBody : "";

        _nextY -= 10f;

        bool firstSection = true;
        string[] lines = orderBody.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                _nextY -= 22f;
                continue;
            }

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                if (!firstSection) _nextY -= 18f;
                firstSection = false;
                var cgSection = AddLabel("", accentColor,
                                         baseFontSize - 2, TextAnchor.UpperLeft,
                                         leftMargin: true);
                var txtSection = _allLabels[_allLabels.Count - 1].text;
                cgSection.alpha = 1f;
                _nextY -= 6f;
                yield return TypewriteText(txtSection, trimmed);
                yield return new WaitForSecondsRealtime(0.2f);
            }
            else
            {
                var cgLine = AddLabel("", bodyColor, baseFontSize,
                                      TextAnchor.UpperLeft, leftMargin: true);
                var txtLine = _allLabels[_allLabels.Count - 1].text;
                cgLine.alpha = 1f;
                _nextY -= 6f;
                yield return TypewriteText(txtLine, trimmed);
                yield return new WaitForSecondsRealtime(bodyLinePause);
            }
        }
    }

    // ── 확인 안내 텍스트 ──────────────────────────────────
    private IEnumerator ShowButton()
    {
        // 구분선
        _nextY -= 8f;
        AddDivider(new Color(0.30f, 0.30f, 0.30f, 1f), 700f);

        var go = new GameObject("PressEnter", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(_panelRect, false);

        var cg   = go.GetComponent<CanvasGroup>();
        cg.alpha = 0f;

        var rect              = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.sizeDelta        = new Vector2(400f, 40f);
        rect.anchoredPosition = new Vector2(0f, _nextY - 16f);

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(go.transform, false);
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        var t                = txtGo.GetComponent<Text>();
        t.text               = "[ Press Enter ]";
        t.font               = _font;
        t.fontSize           = baseFontSize - 4;
        t.color              = headerDimColor;
        t.alignment          = TextAnchor.MiddleCenter;
        t.supportRichText    = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.raycastTarget      = false;

        yield return FadeCg(cg, 0f, 1f, buttonFadeIn);
        StartCoroutine(PulseRoutine(cg));
    }

    // ── 타자기 효과 ───────────────────────────────────────
    private IEnumerator TypewriteText(Text target, string fullText)
    {
        target.text = "";
        foreach (char c in fullText)
        {
            target.text += c;
            yield return new WaitForSecondsRealtime(typewriterSpeed);
        }
    }

    // ── Pulse 애니메이션 ──────────────────────────────────
    private IEnumerator PulseRoutine(CanvasGroup cg)
    {
        float minAlpha = 0.30f;
        float maxAlpha = 1.00f;
        float speed    = 1.3f;
        float t        = 0f;
        while (cg != null && !_confirmed)
        {
            t         += Time.unscaledDeltaTime * speed;
            cg.alpha   = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(t * Mathf.PI) + 1f) * 0.5f);
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
    }

    // ── 캔버스 빌드 ──────────────────────────────────────
    private void BuildCanvas()
    {
        var canvasGo = new GameObject("MissionOrderCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas          = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;

        var scaler                 = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        _rootCg = canvasGo.AddComponent<CanvasGroup>();
        _rootCg.alpha = 0f;

        // 배경 이미지
        if (data != null && data.backgroundSprite != null)
        {
            var bgImgGo   = new GameObject("BgImage", typeof(RectTransform), typeof(RawImage));
            bgImgGo.transform.SetParent(canvasGo.transform, false);
            var bgImgRect = bgImgGo.GetComponent<RectTransform>();
            bgImgRect.anchorMin = Vector2.zero;
            bgImgRect.anchorMax = Vector2.one;
            bgImgRect.offsetMin = Vector2.zero;
            bgImgRect.offsetMax = Vector2.zero;
            var bgRaw           = bgImgGo.GetComponent<RawImage>();
            bgRaw.texture       = data.backgroundSprite.texture;
            bgRaw.color         = new Color(1f, 1f, 1f, data.backgroundAlpha);
            bgRaw.raycastTarget = false;
        }

        // 어두운 오버레이
        bool hasBg   = data != null && data.backgroundSprite != null;
        Color overlay = hasBg
            ? new Color(bgColor.r, bgColor.g, bgColor.b, Mathf.Clamp01(bgColor.a + 0.25f))
            : bgColor;
        var darkGo   = new GameObject("DarkOverlay", typeof(RectTransform), typeof(Image));
        darkGo.transform.SetParent(canvasGo.transform, false);
        var darkRect = darkGo.GetComponent<RectTransform>();
        darkRect.anchorMin = Vector2.zero;
        darkRect.anchorMax = Vector2.one;
        darkRect.offsetMin = Vector2.zero;
        darkRect.offsetMax = Vector2.zero;
        darkGo.GetComponent<Image>().color        = overlay;
        darkGo.GetComponent<Image>().raycastTarget = false;

        // 스캔라인
        if (scanlineAlpha > 0f)
        {
            var slGo   = new GameObject("Scanline", typeof(RectTransform), typeof(RawImage));
            slGo.transform.SetParent(canvasGo.transform, false);
            var slRect = slGo.GetComponent<RectTransform>();
            slRect.anchorMin = Vector2.zero;
            slRect.anchorMax = Vector2.one;
            slRect.offsetMin = Vector2.zero;
            slRect.offsetMax = Vector2.zero;
            var rawImg         = slGo.GetComponent<RawImage>();
            rawImg.texture     = BuildScanlineTex();
            rawImg.uvRect      = new Rect(0f, 0f, 1f, 270f);
            rawImg.color       = new Color(0f, 0f, 0f, scanlineAlpha);
            rawImg.raycastTarget = false;
        }

        // 지도 이미지 (우하단)
        if (data != null && data.mapSprite != null)
        {
            var mapGo   = new GameObject("MapImage", typeof(RectTransform), typeof(Image));
            mapGo.transform.SetParent(canvasGo.transform, false);
            var mapRect = mapGo.GetComponent<RectTransform>();
            mapRect.anchorMin        = new Vector2(1f, 0f);
            mapRect.anchorMax        = new Vector2(1f, 0f);
            mapRect.pivot            = new Vector2(1f, 0f);
            mapRect.sizeDelta        = new Vector2(320f, 220f);
            mapRect.anchoredPosition = new Vector2(-30f, 30f);
            var mapImg              = mapGo.GetComponent<Image>();
            mapImg.sprite           = data.mapSprite;
            mapImg.preserveAspect   = true;
            mapImg.color            = new Color(1f, 1f, 1f, data.mapAlpha);
            mapImg.raycastTarget    = false;
        }

        // 콘텐츠 패널
        const float PW = 800f, PH = 700f;
        var panelGo = new GameObject("Panel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);
        _panelRect           = panelGo.GetComponent<RectTransform>();
        _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRect.pivot     = new Vector2(0.5f, 0.5f);
        _panelRect.sizeDelta = new Vector2(PW, PH);
        _panelRect.anchoredPosition = Vector2.zero;
    }

    // ── 스캔라인 텍스처 ───────────────────────────────────
    private static Texture2D BuildScanlineTex()
    {
        var tex        = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = TextureWrapMode.Repeat;
        tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 1f));
        tex.SetPixel(0, 1, new Color(0f, 0f, 0f, 1f));
        tex.SetPixel(0, 2, Color.clear);
        tex.SetPixel(0, 3, Color.clear);
        tex.Apply();
        return tex;
    }

    // ── AddLabel / AddDivider ─────────────────────────────
    private CanvasGroup AddLabel(string content, Color color, int fontSize,
                                  TextAnchor anchor, bool leftMargin = false,
                                  bool bold = false)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(_panelRect, false);

        var cg            = go.GetComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;

        float xOffset = leftMargin ? -20f : 0f;

        var rect              = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.sizeDelta        = new Vector2(740f, 80f);
        rect.anchoredPosition = new Vector2(xOffset, _nextY);

        var txtGo   = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(go.transform, false);
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        var t                = txtGo.GetComponent<Text>();
        t.text               = content;
        t.font               = _font;
        t.fontSize           = fontSize;
        t.color              = color;
        t.alignment          = anchor;
        t.fontStyle          = bold ? FontStyle.Bold : FontStyle.Normal;
        t.supportRichText    = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.raycastTarget      = false;

        var ol            = txtGo.AddComponent<Outline>();
        ol.effectColor    = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);

        _allLabels.Add((cg, t));
        _nextY -= fontSize + 12f;
        return cg;
    }

    private void AddDivider(Color color, float width = 700f)
    {
        var go   = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_panelRect, false);
        var rect       = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot     = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, 1f);
        rect.anchoredPosition = new Vector2(0f, _nextY);
        go.GetComponent<Image>().color        = color;
        go.GetComponent<Image>().raycastTarget = false;
        _nextY -= 14f;
    }

    // ── 페이드 코루틴 ─────────────────────────────────────
    private IEnumerator FadeInCg(CanvasGroup cg, float duration)
        => FadeCg(cg, 0f, 1f, duration);

    private IEnumerator FadeCg(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        cg.alpha = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed  += Time.unscaledDeltaTime;
            cg.alpha  = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }
}
