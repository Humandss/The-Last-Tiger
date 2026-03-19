using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CrewSubtitleOverlay : MonoBehaviour
{
    public static CrewSubtitleOverlay Instance { get; private set; }

    [Header("Layout")]
    [SerializeField] private float bottomOffset  = 56f;
    [SerializeField] private float lineHeight    = 40f;
    [SerializeField] private float width         = 1200f;
    [SerializeField] private int   fontSize      = 30;
    [SerializeField] private Font  font;

    [Header("Style")]
    [SerializeField] private Color textColor    = Color.white;
    [SerializeField] private Color speakerColor = new Color(0.10f, 0.90f, 0.75f, 1f);
    [SerializeField] private float fadeInSpeed  = 10f;
    [SerializeField] private float fadeOutSpeed = 3f;
    [SerializeField] private float moveSpeed    = 8f;
    [SerializeField] private int   maxEntries   = 4;

    private class SubtitleEntry
    {
        public GameObject   go;
        public RectTransform rect;
        public CanvasGroup  cg;
        public Text         text;
        public float        visibleUntil;
        public float        targetY;
    }

    private Transform       canvasRoot;
    private List<SubtitleEntry> entries = new List<SubtitleEntry>();

    // ─────────────────────────────────────────────
    public static CrewSubtitleOverlay GetOrCreate()
    {
        if (Instance != null) return Instance;

        CrewSubtitleOverlay found = FindAnyObjectByType<CrewSubtitleOverlay>();
        if (found != null) return found;

        GameObject go = new GameObject("CrewSubtitleOverlay");
        DontDestroyOnLoad(go);
        return go.AddComponent<CrewSubtitleOverlay>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCanvas();
    }

    // ─────────────────────────────────────────────
    public void ShowLine(string speaker, string line, float duration)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        if (canvasRoot == null) BuildCanvas();

        // 오래된 항목 초과 시 가장 오래된 것 제거
        if (entries.Count >= maxEntries)
            RemoveEntry(entries[0]);

        // 기존 항목들 위로 올리기
        for (int i = 0; i < entries.Count; i++)
            entries[i].targetY += lineHeight;

        // 새 항목 추가
        SubtitleEntry e = CreateEntry();
        string hex = ColorUtility.ToHtmlStringRGB(speakerColor);
        e.text.text    = $"<color=#{hex}>{speaker}:</color> {line}";
        e.targetY      = bottomOffset;
        e.rect.anchoredPosition = new Vector2(0f, bottomOffset);
        e.cg.alpha     = 0f;
        e.visibleUntil = Time.unscaledTime + Mathf.Max(0.25f, duration);
        entries.Add(e);
    }

    // ─────────────────────────────────────────────
    private void Update()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            SubtitleEntry e = entries[i];

            // Y 이동
            Vector2 pos = e.rect.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, e.targetY, moveSpeed * Time.unscaledDeltaTime);
            e.rect.anchoredPosition = pos;

            // 알파
            bool visible = Time.unscaledTime <= e.visibleUntil;
            float targetAlpha = visible ? 1f : 0f;
            float speed       = visible ? fadeInSpeed : fadeOutSpeed;
            e.cg.alpha = Mathf.MoveTowards(e.cg.alpha, targetAlpha, speed * Time.unscaledDeltaTime);

            // 완전히 사라지면 제거
            if (!visible && e.cg.alpha <= 0.01f)
                RemoveEntry(e);
        }
    }

    // ─────────────────────────────────────────────
    private void RemoveEntry(SubtitleEntry e)
    {
        entries.Remove(e);
        if (e.go != null) Destroy(e.go);
    }

    private SubtitleEntry CreateEntry()
    {
        GameObject go = new GameObject("SubtitleEntry", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvasRoot, false);

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        cg.interactable  = false;
        cg.blocksRaycasts = false;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot     = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(width, lineHeight);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text t = textGo.GetComponent<Text>();
        t.supportRichText  = true;
        t.alignment        = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.fontSize  = fontSize;
        t.color     = textColor;
        t.raycastTarget = false;
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Outline ol = textGo.AddComponent<Outline>();
        ol.effectColor    = new Color(0f, 0f, 0f, 0.9f);
        ol.effectDistance = new Vector2(1f, -1f);

        return new SubtitleEntry { go = go, rect = rect, cg = cg, text = t };
    }

    private void BuildCanvas()
    {
        Transform old = transform.Find("SubtitleCanvas");
        if (old != null) Destroy(old.gameObject);

        GameObject canvasGo = new GameObject("SubtitleCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        RectTransform rootRect = canvasGo.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        canvasRoot = canvasGo.transform;
    }
}
