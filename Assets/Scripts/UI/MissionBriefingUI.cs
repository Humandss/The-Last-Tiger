using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 미션 브리핑 UI.
/// 연출 순서:
///   1. 암전 페이드인  +  BGM / 앰비언트 페이드인
///   2. 헤더 순차 등장
///   3. 상황 타자기 효과 (SFX)
///   4. 임무 목표 순차 등장
///   5. 전차장 발언
///   6. "출격" 버튼 페이드인
///   7. 클릭 → 오디오 페이드아웃 + 암전 → OnBriefingComplete
/// </summary>
public class MissionBriefingUI : MonoBehaviour
{
    // ── 데이터 ───────────────────────────────────────────
    [Header("데이터")]
    [SerializeField] private MissionBriefingData data;
    public MissionBriefingData Data => data;
    [SerializeField] private float _nextY = 10f;
    // ── 타이밍 ──────────────────────────────────────────
    [Header("타이밍 (초)")]
    [SerializeField] private float overlayFadeIn      = 1.0f;
    [SerializeField] private float overlayFadeOut     = 0.8f;
    [SerializeField] private float headerStagger      = 0.55f;
    [SerializeField] private float sectionPause       = 0.5f;
    [SerializeField] private float typewriterInterval = 0.035f;
    [SerializeField] private float situationLinePause = 0.6f;
    [SerializeField] private float objectiveStagger   = 0.5f;
    [SerializeField] private float buttonFadeIn       = 0.6f;

    // ── 스타일 ──────────────────────────────────────────
    [Header("스타일")]
    [SerializeField] private Font  customFont;                                              
    [SerializeField] private Color bgColor          = new Color(0.04f, 0.04f, 0.06f, 0.93f);
    [SerializeField] private Color accentColor      = new Color(0.85f, 0.72f, 0.20f, 1f);
    [SerializeField] private Color headerDimColor   = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color bodyColor        = new Color(0.88f, 0.88f, 0.88f, 1f);
    [SerializeField] private Color objectiveColor   = new Color(0.95f, 0.95f, 0.90f, 1f);
    [SerializeField] private Color remarkSpeakerCol = new Color(0.30f, 0.85f, 0.70f, 1f);
    [SerializeField] private int   baseFontSize     = 30;
    [SerializeField] [Range(0f, 0.4f)] private float scanlineAlpha = 0.12f;  // 0 = 스캔라인 없음

    // ── 입력 차단 ────────────────────────────────────────
    [Header("브리핑 중 비활성화할 스크립트")]
    [SerializeField] private MonoBehaviour[] disableDuring;

    // ── 이벤트 ──────────────────────────────────────────
    [Header("이벤트")]
    public UnityEvent OnBriefingComplete;

    // ── 내부 상태 ────────────────────────────────────────
    private Canvas      _canvas;
    private CanvasGroup _rootCg;
    private Font        _font;
    private AudioSource _bgmSrc;
    private AudioSource _ambientSrc;
    private AudioSource _typewriterSrc;

    // ─────────────────────────────────────────────────────
    private void Awake()
    {
        _font = (customFont != null)
            ? customFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        SetupAudio();
        BuildCanvas();
    }

    private void Start()
    {
        foreach (var mb in disableDuring)
            if (mb) mb.enabled = false;

        StartCoroutine(PlaySequence());
    }

    // ── 오디오 초기화 ─────────────────────────────────────
    private void SetupAudio()
    {
        _bgmSrc            = gameObject.AddComponent<AudioSource>();
        _bgmSrc.loop       = true;
        _bgmSrc.playOnAwake = false;
        _bgmSrc.volume     = 0f;

        _ambientSrc            = gameObject.AddComponent<AudioSource>();
        _ambientSrc.loop       = true;
        _ambientSrc.playOnAwake = false;
        _ambientSrc.volume     = 0f;

        _typewriterSrc            = gameObject.AddComponent<AudioSource>();
        _typewriterSrc.loop       = false;
        _typewriterSrc.playOnAwake = false;
        _typewriterSrc.volume     = data ? data.typewriterVolume : 0.5f;
    }

    // ── 메인 연출 코루틴 ──────────────────────────────────
    private IEnumerator PlaySequence()
    {
        // BGM + 앰비언트 시작 (페이드인과 동시)
        if (data != null && data.bgmClip)
        {
            _bgmSrc.clip = data.bgmClip;
            _bgmSrc.Play();
            StartCoroutine(FadeAudio(_bgmSrc, 0f, data.bgmVolume, overlayFadeIn));
        }
        if (data != null && data.ambientClip)
        {
            _ambientSrc.clip = data.ambientClip;
            _ambientSrc.Play();
            StartCoroutine(FadeAudio(_ambientSrc, 0f, data.ambientVolume, overlayFadeIn));
        }

        // ① 암전 페이드인
        yield return FadeCg(_rootCg, 0f, 1f, overlayFadeIn);

        // ② 헤더
        yield return ShowHeader();
        yield return new WaitForSecondsRealtime(sectionPause);

        // ③ 상황 (타자기)
        yield return ShowSection("[ 상황 ]",
            data ? data.situationLines : new string[0],
            bodyColor, typewriter: true, linePause: situationLinePause);
        yield return new WaitForSecondsRealtime(sectionPause);

        // ④ 임무 목표
        yield return ShowSection("[ 임무 ]",
            BuildNumberedList(data ? data.objectives : new string[0]),
            objectiveColor, typewriter: false, linePause: objectiveStagger, lineSpacing: 18f);
        yield return new WaitForSecondsRealtime(sectionPause);

        // ⑤ 전차장 발언
        yield return ShowRemarks();
        yield return new WaitForSecondsRealtime(0.4f);

        // ⑥ 출격 버튼
        yield return ShowButton();
    }

    // ── ② 헤더 ───────────────────────────────────────────
    private IEnumerator ShowHeader()
    {
        if (data == null) yield break;

        // 로고 — _nextY 흐름에 맞게 배치 후 직접 갱신
        if (data.logoSprite != null)
        {
            float size = data.logoSize;

            var logoGo   = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            logoGo.transform.SetParent(_panelRect, false);
            var logoCg   = logoGo.GetComponent<CanvasGroup>();
            logoCg.alpha          = 0f;
            logoCg.interactable   = false;
            logoCg.blocksRaycasts = false;
            var logoRect = logoGo.GetComponent<RectTransform>();
            logoRect.anchorMin        = new Vector2(0.5f, 1f);
            logoRect.anchorMax        = new Vector2(0.5f, 1f);
            logoRect.pivot            = new Vector2(0.5f, 1f);
            logoRect.sizeDelta        = new Vector2(size, size);
            logoRect.anchoredPosition = new Vector2(0f, _nextY);  // 현재 흐름 위치에 배치
            var logoImg            = logoGo.GetComponent<Image>();
            logoImg.sprite         = data.logoSprite;
            logoImg.preserveAspect = true;
            logoImg.raycastTarget  = false;
            // 검은 배경 제거: Screen 블렌드 (흰색 로고에 적합)
            logoImg.material = new Material(Shader.Find("UI/Default"));
            logoImg.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            logoImg.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);

            _nextY -= size + 14f;  // 로고 높이만큼 _nextY 직접 갱신

            yield return FadeInCg(logoCg, headerStagger);
            yield return new WaitForSecondsRealtime(headerStagger * 0.3f);
        }

        // 분류 라인 — 이후는 모두 NextY() 흐름
        var cgClass = AddLabel(data.classification, headerDimColor, baseFontSize - 2,
                               TextAnchor.UpperCenter, y: NextY());
        yield return FadeInCg(cgClass, headerStagger * 0.5f);
        yield return new WaitForSecondsRealtime(headerStagger);

        _nextY -= 6f;
        AddDivider(y: NextY(), color: accentColor, width: 700f);
        yield return new WaitForSecondsRealtime(0.2f);

        _nextY -= 4f;
        var cgMission = AddLabel(data.missionName, accentColor, baseFontSize + 8,
                                 TextAnchor.UpperCenter, y: NextY(), bold: true);
        yield return FadeInCg(cgMission, headerStagger);
        yield return new WaitForSecondsRealtime(headerStagger * 0.5f);

        _nextY -= 4f;
        var cgDate = AddLabel(data.dateLocation, headerDimColor, baseFontSize - 1,
                              TextAnchor.UpperCenter, y: NextY());
        yield return FadeInCg(cgDate, headerStagger);
        yield return new WaitForSecondsRealtime(0.3f);

        _nextY -= 6f;
        AddDivider(y: NextY(), color: new Color(0.35f, 0.35f, 0.35f, 1f), width: 700f);
    }

    // ── ③④ 섹션 텍스트 ───────────────────────────────────
    private IEnumerator ShowSection(string title, string[] lines, Color lineColor,
                                    bool typewriter, float linePause, float lineSpacing = 38f)
    {
        float startY = NextY();
        var cgTitle = AddLabel(title, accentColor, baseFontSize - 1,
                               TextAnchor.UpperLeft, y: startY, leftMargin: true);
        yield return FadeInCg(cgTitle, 0.3f);
        yield return new WaitForSecondsRealtime(0.25f);
        _nextY -= 36f;

        foreach (string line in lines)
        {
            float y = NextY();
            if (typewriter)
            {
                var cgLine = AddLabel("", lineColor, baseFontSize,
                                      TextAnchor.UpperLeft, y: y, leftMargin: true);
                cgLine.alpha = 1f;
                yield return Typewrite(GetTextComponent(cgLine), line);
            }
            else
            {
                var cgLine = AddLabel(line, lineColor, baseFontSize,
                                      TextAnchor.UpperLeft, y: y, leftMargin: true);
                yield return FadeInCg(cgLine, 0.3f);
            }
            yield return new WaitForSecondsRealtime(linePause);
            _nextY -= lineSpacing;
        }
    }

    // ── ⑤ 전차장 발언 ────────────────────────────────────
    private IEnumerator ShowRemarks()
    {
        if (data == null || data.remarks == null) yield break;

        float startY = NextY() - 10f;
        _nextY = startY;
        AddDivider(y: startY + 10f, color: new Color(0.35f, 0.35f, 0.35f, 1f), width: 700f);
        yield return new WaitForSecondsRealtime(0.3f);

        foreach (var remark in data.remarks)
        {
            if (string.IsNullOrWhiteSpace(remark.line)) continue;

            float y = NextY();
            string rich = $"<color=#{ColorUtility.ToHtmlStringRGB(remarkSpeakerCol)}>" +
                          $"{remark.speaker}:</color>  {remark.line}";

            var cgRemark = AddLabel(rich, bodyColor, baseFontSize,
                                    TextAnchor.UpperLeft, y: y, leftMargin: true, richText: true);
            yield return FadeInCg(cgRemark, 0.4f);
            yield return new WaitForSecondsRealtime(remark.pauseAfter);
            _nextY -= 40f;
        }
    }

    // ── ⑥ 출격 버튼 ──────────────────────────────────────
    private IEnumerator ShowButton()
    {
        string label = data != null ? data.startButtonText : "출격";

        var btnGo   = new GameObject("StartButton", typeof(RectTransform), typeof(CanvasGroup));
        btnGo.transform.SetParent(_panelRect, false);

        var btnCg   = btnGo.GetComponent<CanvasGroup>();
        btnCg.alpha = 0f;

        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin        = new Vector2(0.5f, 1f);
        btnRect.anchorMax        = new Vector2(0.5f, 1f);
        btnRect.pivot            = new Vector2(0.5f, 1f);
        btnRect.sizeDelta        = new Vector2(200f, 48f);
        btnRect.anchoredPosition = new Vector2(0f, _nextY - 20f);

        var imgGo   = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(btnGo.transform, false);
        var imgRect = imgGo.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;
        imgGo.GetComponent<Image>().color = accentColor;

        var txtGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(btnGo.transform, false);
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        var txt           = txtGo.GetComponent<Text>();
        txt.text          = label;
        txt.font          = _font;
        txt.fontSize      = baseFontSize + 2;
        txt.fontStyle     = FontStyle.Bold;
        txt.color         = new Color(0.05f, 0.05f, 0.05f, 1f);
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = imgGo.GetComponent<Image>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.90f, 0.45f, 1f);
        colors.pressedColor     = new Color(0.65f, 0.55f, 0.10f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(OnStartClicked);

        yield return FadeCg(btnCg, 0f, 1f, buttonFadeIn);
    }

    private void OnStartClicked()  => StartCoroutine(FinishBriefing());

    private IEnumerator FinishBriefing()
    {
        // 오디오 페이드아웃과 화면 암전 동시 진행
        StartCoroutine(FadeAudio(_bgmSrc,     _bgmSrc.volume,     0f, overlayFadeOut));
        StartCoroutine(FadeAudio(_ambientSrc, _ambientSrc.volume, 0f, overlayFadeOut));
        yield return FadeCg(_rootCg, 1f, 0f, overlayFadeOut);

        foreach (var mb in disableDuring)
            if (mb) mb.enabled = true;

        OnBriefingComplete?.Invoke();
    }

    // ── 캔버스 빌드 ──────────────────────────────────────
    private RectTransform _panelRect;
    private float NextY() => _nextY;

    private void BuildCanvas()
    {
        // 루트 캔버스
        var canvasGo = new GameObject("BriefingCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas          = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        var scaler                 = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        _rootCg       = canvasGo.AddComponent<CanvasGroup>();
        _rootCg.alpha = 0f;

        // ── 레이어 1: 배경 이미지 ──────────────────────────
        bool hasBg = data != null && data.backgroundSprite != null;
        if (hasBg)
        {
            var bgImgGo   = new GameObject("BgImage", typeof(RectTransform), typeof(RawImage));
            bgImgGo.transform.SetParent(canvasGo.transform, false);
            var bgImgRect = bgImgGo.GetComponent<RectTransform>();
            bgImgRect.anchorMin = Vector2.zero;
            bgImgRect.anchorMax = Vector2.one;
            bgImgRect.offsetMin = Vector2.zero;
            bgImgRect.offsetMax = Vector2.zero;
            var bgRaw             = bgImgGo.GetComponent<RawImage>();
            bgRaw.texture         = data.backgroundSprite.texture;
            bgRaw.uvRect          = new Rect(0f, 0f, 1f, 1f);   // 텍스처를 rect에 꽉 채움
            bgRaw.color           = new Color(1f, 1f, 1f, data.backgroundAlpha);
            bgRaw.raycastTarget   = false;
        }

        // ── 레이어 2: 어두운 오버레이 ──────────────────────
        var bgGo   = new GameObject("DarkOverlay", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        // 배경 이미지가 있으면 오버레이를 좀 더 어둡게
        Color overlayCol = hasBg
            ? new Color(bgColor.r, bgColor.g, bgColor.b, Mathf.Clamp01(bgColor.a + 0.25f))
            : bgColor;
        bgGo.GetComponent<Image>().color       = overlayCol;
        bgGo.GetComponent<Image>().raycastTarget = false;

        // ── 레이어 3: 스캔라인 오버레이 ────────────────────
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
            rawImg.uvRect      = new Rect(0f, 0f, 1f, 270f);  // 1080px / 4px = 270 타일
            rawImg.color       = new Color(0f, 0f, 0f, scanlineAlpha);
            rawImg.raycastTarget = false;
        }

        // ── 레이어 4: 지도 이미지 (우하단) ─────────────────
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
            var mapImg           = mapGo.GetComponent<Image>();
            mapImg.sprite        = data.mapSprite;
            mapImg.preserveAspect = true;
            mapImg.color         = new Color(1f, 1f, 1f, data.mapAlpha);
            mapImg.raycastTarget = false;
        }

        // ── 레이어 5: 콘텐츠 패널 ──────────────────────────
        var panelGo  = new GameObject("Panel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);
        _panelRect           = panelGo.GetComponent<RectTransform>();
        _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRect.pivot     = new Vector2(0.5f, 0.5f);
        _panelRect.sizeDelta = new Vector2(800f, 700f);
        _panelRect.anchoredPosition = Vector2.zero;

        _canvas = canvas;
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

    // ── AddLabel ─────────────────────────────────────────
    private readonly List<(CanvasGroup cg, Text text)> _allLabels
        = new List<(CanvasGroup, Text)>();

    private CanvasGroup AddLabel(string content, Color color, int fontSize,
                                  TextAnchor anchor, float y,
                                  bool leftMargin = false, bool bold = false,
                                  bool richText   = false)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(_panelRect, false);

        var cg            = go.GetComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;

        var rect              = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.sizeDelta        = new Vector2(740f, 80f);
        rect.anchoredPosition = new Vector2(leftMargin ? -20f : 0f, y);

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
        t.supportRichText    = richText;
        t.fontStyle          = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.raycastTarget      = false;

        var ol            = txtGo.AddComponent<Outline>();
        ol.effectColor    = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);

        _allLabels.Add((cg, t));
        _nextY -= fontSize + 12f;
        return cg;
    }

    private Text GetTextComponent(CanvasGroup cg)
    {
        foreach (var pair in _allLabels)
            if (pair.cg == cg) return pair.text;
        return null;
    }

    private void AddDivider(float y, Color color, float width = 700f)
    {
        var go   = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_panelRect, false);
        var rect       = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot     = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        go.GetComponent<Image>().color       = color;
        go.GetComponent<Image>().raycastTarget = false;
        _nextY -= 14f;
    }

    // ── 타자기 코루틴 ─────────────────────────────────────
    private IEnumerator Typewrite(Text target, string fullText)
    {
        if (target == null) yield break;
        target.text = "";
        foreach (char c in fullText)
        {
            target.text += c;
            // 공백·구두점은 SFX 생략
            if (_typewriterSrc != null && data != null && data.typewriterClip != null
                && !char.IsWhiteSpace(c) && c != '.' && c != ',')
            {
                _typewriterSrc.PlayOneShot(data.typewriterClip, data.typewriterVolume);
            }
            yield return new WaitForSecondsRealtime(typewriterInterval);
        }
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

    private IEnumerator FadeAudio(AudioSource src, float from, float to, float duration)
    {
        if (src == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed   += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        src.volume = to;
        if (to <= 0f) src.Stop();
    }

    // ── 번호 목록 ─────────────────────────────────────────
    private static string[] BuildNumberedList(string[] items)
    {
        if (items == null) return new string[0];
        var result = new string[items.Length];
        for (int i = 0; i < items.Length; i++)
            result[i] = $"{i + 1}.  {items[i]}";
        return result;
    }
}
