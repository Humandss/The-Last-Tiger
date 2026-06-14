using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 조작 교범 오버레이.
/// - 게임 중 ESC로 토글 (보는 동안 일시정지)
/// - 게임 시작 작전지령 직후 1회 표시(OpenIntro)도 담당
/// 씬에 하나 배치하고, MissionOrderUI가 OpenIntro로 호출한다.
/// </summary>
public class ControlsManualUI : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private KeyCode toggleKey   = KeyCode.Escape;
    [SerializeField] private float   introMinShow = 5f;   // 게임 시작 표시 시 최소 유지(초)

    [Header("스타일")]
    [SerializeField] private Font  customFont;
    [SerializeField] private Color bgColor        = new Color(0.04f, 0.04f, 0.06f, 0.93f);
    [SerializeField] private Color accentColor    = new Color(0.85f, 0.72f, 0.20f, 1f);
    [SerializeField] private Color headerDimColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color bodyColor      = new Color(0.88f, 0.88f, 0.88f, 1f);
    [SerializeField] private int   baseFontSize   = 28;
    [SerializeField] [Range(0f, 0.4f)] private float scanlineAlpha = 0.12f;
    [SerializeField] private float fadeDuration = 0.4f;

    private Font          _font;
    private CanvasGroup   _rootCg;
    private RectTransform _panelRect;
    private bool          _open;
    private bool          _busy;        // 페이드 중
    private bool          _introMode;   // 게임 시작 1회 표시 중(ESC 토글 잠금)
    private Action        _onIntroClosed;
    private Text          _hintText;    // 마지막 안내 줄(모드에 따라 문구 변경)

    private void Awake()
    {
        _font = (customFont != null)
            ? customFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildCanvas();
        _rootCg.alpha = 0f;
        _rootCg.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_busy || _introMode) return;
        if (!Input.GetKeyDown(toggleKey)) return;

        if (_open) StartCoroutine(CloseRoutine(null));
        else       StartCoroutine(OpenRoutine(false));
    }

    /// <summary>게임 시작 작전지령 직후 1회 표시. 닫히면 onClosed 호출(게임 재개용).</summary>
    public void OpenIntro(Action onClosed)
    {
        _onIntroClosed = onClosed;
        StartCoroutine(OpenRoutine(true));
    }

    private IEnumerator OpenRoutine(bool intro)
    {
        _busy      = true;
        _introMode = intro;
        _open      = true;
        Time.timeScale = 0f;

        if (_hintText != null)
            _hintText.text = intro
                ? "▸ 아무 키나 눌러 전투 개시 ◂\n<size=18><color=#9A948A>※ 전투 중 ESC로 이 교범을 다시 볼 수 있습니다</color></size>"
                : "▸ ESC 또는 아무 키로 닫기 ◂";

        _rootCg.gameObject.SetActive(true);
        yield return Fade(_rootCg, 0f, 1f, fadeDuration);
        _busy = false;

        if (!intro) yield break;

        // 게임 시작 표시: 최소 유지 시간 후 아무 키(음성 V 제외)
        float t = 0f;
        while (t < introMinShow) { t += Time.unscaledDeltaTime; yield return null; }
        while (true)
        {
            if (Input.anyKeyDown && !Input.GetKey(KeyCode.V)) break;
            yield return null;
        }
        yield return CloseRoutine(_onIntroClosed);
    }

    private IEnumerator CloseRoutine(Action onClosed)
    {
        _busy      = true;
        _introMode = false;
        yield return Fade(_rootCg, 1f, 0f, fadeDuration);
        _rootCg.gameObject.SetActive(false);
        _open = false;
        Time.timeScale = 1f;
        _busy = false;
        onClosed?.Invoke();
    }

    private IEnumerator Fade(CanvasGroup cg, float from, float to, float dur)
    {
        cg.alpha = from;
        float e = 0f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, e / dur);
            yield return null;
        }
        cg.alpha = to;
    }

    // ── 캔버스 빌드 (어두운 배경 + 스캔라인 + 교범 패널) ──
    private void BuildCanvas()
    {
        var canvasGo = new GameObject("ControlsManualCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas          = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20500;

        var scaler                 = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        _rootCg = canvasGo.AddComponent<CanvasGroup>();

        // 어두운 오버레이
        var darkGo = new GameObject("DarkOverlay", typeof(RectTransform), typeof(Image));
        darkGo.transform.SetParent(canvasGo.transform, false);
        StretchFull(darkGo.GetComponent<RectTransform>());
        var darkImg            = darkGo.GetComponent<Image>();
        darkImg.color          = bgColor;
        darkImg.raycastTarget  = false;

        // 스캔라인
        if (scanlineAlpha > 0f)
        {
            var slGo = new GameObject("Scanline", typeof(RectTransform), typeof(RawImage));
            slGo.transform.SetParent(canvasGo.transform, false);
            StretchFull(slGo.GetComponent<RectTransform>());
            var raw            = slGo.GetComponent<RawImage>();
            raw.texture        = BuildScanlineTex();
            raw.uvRect         = new Rect(0f, 0f, 1f, 270f);
            raw.color          = new Color(0f, 0f, 0f, scanlineAlpha);
            raw.raycastTarget  = false;
        }

        // 교범 패널
        var panelGo = new GameObject("Panel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);
        _panelRect                  = panelGo.GetComponent<RectTransform>();
        _panelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        _panelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        _panelRect.pivot            = new Vector2(0.5f, 0.5f);
        _panelRect.sizeDelta        = new Vector2(1040f, 800f);
        _panelRect.anchoredPosition = Vector2.zero;

        BuildManualPanel();
    }

    private static void StretchFull(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    // ── 교범 내용 ──
    private void BuildManualPanel()
    {
        const string K = "#D9B23A";
        var prect = _panelRect;
        Color body = bodyColor;
        Color dim  = headerDimColor;

        AddManualBorder(prect, 1000f, 760f, new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f));

        AddManualText(prect, "기밀 · 제4기갑군 — 기갑 승무원 제식 교범", dim,
                      baseFontSize - 8, TextAnchor.UpperCenter,
                      new Vector2(0f, 345f), new Vector2(940f, 26f));

        AddManualText(prect, "Panzerkampfwagen VI Tiger    교범", accentColor,
                      baseFontSize + 4, TextAnchor.UpperCenter,
                      new Vector2(0f, 315f), new Vector2(940f, 44f), bold: true);

        AddManualDivider(prect, new Vector2(0f, 278f), 940f, accentColor);

        string hull =
            $"<b><color={K}>차체 기동</color></b>\n" +
            $"<color={K}>W / S</color>   전·후진\n" +
            $"<color={K}>A / D</color>   좌·우 선회\n" +
            $"<color={K}>Space</color>   급정지\n" +
            $"<color={K}>Shift / Ctrl</color>   속도 강·약";

        string gun =
            $"<b><color={K}>주포·사격</color></b>\n" +
            $"<color={K}>F</color>   조준\n" +
            $"<color={K}>좌클릭</color>   사격\n" +
            $"<color={K}>E / Q</color>   사거리 ±\n" +
            $"<color={K}>휠클릭</color>   목표 지정\n" +
            $"<color={K}>T</color>   사격 중지\n" +
            $"<color={K}>Y</color>   차체 정렬";

        string load =
            $"<b><color={K}>장전</color></b>\n" +
            $"<color={K}>1</color>   철갑탄 (AP)\n" +
            $"<color={K}>2</color>   고폭탄 (HE)\n" +
            $"<color={K}>R</color>   직전 탄종 재장전";

        string view =
            $"<b><color={K}>시점</color></b>\n" +
            $"<color={K}>우클릭</color>   조준경 줌\n" +
            $"<color={K}>Tab</color>   전차 내부 상태";

        AddManualText(prect, hull, body, baseFontSize - 4, TextAnchor.UpperCenter,
                      new Vector2(-330f, 220f), new Vector2(400f, 150f));
        AddManualText(prect, gun, body, baseFontSize - 4, TextAnchor.UpperCenter,
                      new Vector2(330f, 220f), new Vector2(400f, 190f));
        AddManualText(prect, load, body, baseFontSize - 4, TextAnchor.UpperCenter,
                      new Vector2(-330f, -35f), new Vector2(400f, 120f));
        AddManualText(prect, view, body, baseFontSize - 4, TextAnchor.UpperCenter,
                      new Vector2(330f, -35f), new Vector2(400f, 90f));

        AddManualDivider(prect, new Vector2(0f, -185f), 940f, dim);

        string voice =
            $"<b><color={K}>음성 지휘</color></b>   <color=#9A948A>※ 서버 연결 필요</color>\n" +
            $"<color={K}>V</color> 누르고 말하기   \"포수, 사격\" · \"철갑탄 장전\"";
        AddManualText(prect, voice, body, baseFontSize - 4, TextAnchor.UpperCenter,
                      new Vector2(0f, -225f), new Vector2(940f, 60f));

        _hintText = AddManualText(prect, "▸ ESC 또는 아무 키로 닫기 ◂", dim,
                      baseFontSize - 6, TextAnchor.UpperCenter,
                      new Vector2(0f, -308f), new Vector2(940f, 64f));
    }

    // ── 단일 리치텍스트 라벨 (pivot 상단 = anchoredPos.y가 글자 상단) ──
    private Text AddManualText(Transform parent, string content, Color color,
                               int fontSize, TextAnchor anchor,
                               Vector2 anchoredPos, Vector2 size, bool bold = false)
    {
        var go = new GameObject("MText", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect              = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.sizeDelta        = size;
        rect.anchoredPosition = anchoredPos;

        var t                = go.GetComponent<Text>();
        t.text               = content;
        t.font               = _font;
        t.fontSize           = fontSize;
        t.color              = color;
        t.alignment          = anchor;
        t.supportRichText    = true;
        t.fontStyle          = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.raycastTarget      = false;

        var ol            = go.AddComponent<Outline>();
        ol.effectColor    = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    private void AddManualDivider(Transform parent, Vector2 anchoredPos, float width, Color color)
    {
        var go = new GameObject("MDiv", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect              = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = new Vector2(width, 1.5f);
        rect.anchoredPosition = anchoredPos;
        var img           = go.GetComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
    }

    private void AddManualBorder(Transform parent, float w, float h, Color color)
    {
        const float thick = 1.5f;
        void Bar(Vector2 pos, Vector2 sz)
        {
            var go = new GameObject("MBorder", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r              = go.GetComponent<RectTransform>();
            r.anchorMin        = new Vector2(0.5f, 0.5f);
            r.anchorMax        = new Vector2(0.5f, 0.5f);
            r.pivot            = new Vector2(0.5f, 0.5f);
            r.sizeDelta        = sz;
            r.anchoredPosition = pos;
            var img           = go.GetComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
        }
        Bar(new Vector2(0f, h * 0.5f),  new Vector2(w, thick));
        Bar(new Vector2(0f, -h * 0.5f), new Vector2(w, thick));
        Bar(new Vector2(-w * 0.5f, 0f), new Vector2(thick, h));
        Bar(new Vector2(w * 0.5f, 0f),  new Vector2(thick, h));
    }

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
}
