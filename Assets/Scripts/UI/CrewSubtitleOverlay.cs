using UnityEngine;
using UnityEngine.UI;

public class CrewSubtitleOverlay : MonoBehaviour
{
    public static CrewSubtitleOverlay Instance { get; private set; }

    [Header("Layout")]
    [SerializeField] private float bottomOffset = 56f;
    [SerializeField] private float width = 1200f;
    [SerializeField] private int fontSize = 30;
    [SerializeField] private Font font;

    [Header("Style")]
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color speakerColor = new Color(0.10f, 0.90f, 0.75f, 1f);
    [SerializeField] private float fadeInSpeed = 10f;
    [SerializeField] private float fadeOutSpeed = 5f;

    private CanvasGroup canvasGroup;
    private Text subtitleText;
    private float visibleUntil = -999f;

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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
    }

    private void Update()
    {
        if (canvasGroup == null) return;

        bool visible = Time.unscaledTime <= visibleUntil;
        float target = visible ? 1f : 0f;
        float speed = visible ? fadeInSpeed : fadeOutSpeed;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, speed * Time.unscaledDeltaTime);
    }

    public void ShowLine(string speaker, string line, float duration)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        if (subtitleText == null) BuildUI();

        string speakerHex = ColorUtility.ToHtmlStringRGB(speakerColor);
        subtitleText.text = $"<color=#{speakerHex}>{speaker}:</color> {line}";
        visibleUntil = Time.unscaledTime + Mathf.Max(0.25f, duration);
    }

    private void BuildUI()
    {
        Transform old = transform.Find("SubtitleCanvas");
        if (old != null) Destroy(old.gameObject);

        GameObject canvasGo = new GameObject("SubtitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasGo.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform rootRect = canvasGo.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject textGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0f);
        textRect.anchorMax = new Vector2(0.5f, 0f);
        textRect.pivot = new Vector2(0.5f, 0f);
        textRect.anchoredPosition = new Vector2(0f, bottomOffset);
        textRect.sizeDelta = new Vector2(width, 80f);

        subtitleText = textGo.GetComponent<Text>();
        subtitleText.supportRichText = true;
        subtitleText.alignment = TextAnchor.MiddleCenter;
        subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitleText.verticalOverflow = VerticalWrapMode.Overflow;
        subtitleText.fontSize = fontSize;
        subtitleText.color = textColor;
        subtitleText.raycastTarget = false;
        subtitleText.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subtitleText.text = string.Empty;

        Outline ol = textGo.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ol.effectDistance = new Vector2(1f, -1f);
    }
}
