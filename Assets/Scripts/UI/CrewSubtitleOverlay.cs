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

    [Header("Queue")]
    [SerializeField] private float queueGapSeconds = 0.3f;   // 메시지 사이 간격

    private class SubtitleEntry
    {
        public GameObject   go;
        public RectTransform rect;
        public CanvasGroup  cg;
        public Text         text;
        public float        visibleUntil;
        public float        targetY;
    }

    private struct QueuedLine
    {
        public string speaker, line;
        public float  duration;
    }

    private Transform           canvasRoot;
    private List<SubtitleEntry> entries   = new List<SubtitleEntry>();
    private Queue<QueuedLine>   lineQueue = new Queue<QueuedLine>();
    private float               nextShowTime = 0f;

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
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 씬 전환 시 차단 플래그 초기화
        _silenced = false;
        lineQueue.Clear();
    }

    // ─────────────────────────────────────────────
    // 외부에서 호출 — 큐에 넣고 순서대로 출력
    public void ShowLine(string speaker, string line, float duration)
    {
        if (_silenced) return; // 탄약고 폭발 후 차단
        if (string.IsNullOrWhiteSpace(line)) return;
        lineQueue.Enqueue(new QueuedLine { speaker = speaker, line = line, duration = duration });
    }

    /// <summary>
    /// 탄약고 폭발 등으로 모든 자막을 즉시 제거하고 이후 출력도 차단합니다.
    /// </summary>
    private bool _silenced = false;

    public void SilenceAll()
    {
        _silenced = true;
        lineQueue.Clear();
        for (int i = entries.Count - 1; i >= 0; i--)
            RemoveEntry(entries[i]);
    }

    // 실제 화면에 추가
    private void DisplayLine(string speaker, string line, float duration)
    {
        if (canvasRoot == null) BuildCanvas();

        if (entries.Count >= maxEntries)
            RemoveEntry(entries[0]);

        for (int i = 0; i < entries.Count; i++)
            entries[i].targetY += lineHeight;

        SubtitleEntry e = CreateEntry();
        string hex = ColorUtility.ToHtmlStringRGB(speakerColor);
        e.text.text    = $"<color=#{hex}>{speaker}:</color> {line}";
        e.targetY      = bottomOffset;
        e.rect.anchoredPosition = new Vector2(0f, bottomOffset);
        e.cg.alpha     = 0f;
        e.visibleUntil = Time.unscaledTime + Mathf.Max(0.25f, duration);
        entries.Add(e);

        // 다음 메시지는 이 메시지가 끝난 후 + 간격
        nextShowTime = e.visibleUntil + queueGapSeconds;
    }

    // ─────────────────────────────────────────────
    private void Update()
    {
        // 큐에서 순서대로 꺼내 표시
        if (lineQueue.Count > 0 && Time.unscaledTime >= nextShowTime)
        {
            var q = lineQueue.Dequeue();
            DisplayLine(q.speaker, q.line, q.duration);
        }

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
