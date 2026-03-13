using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class RangeBarAutoSetup : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private GunnerController gunner;

    [Header("Build")]
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField, Min(64f)] private float tapeWidth = 150f;
    [SerializeField, Min(120f)] private float tapeHeight = 320f;
    [SerializeField, Min(0f)] private float rightOffset = 36f;
    [SerializeField, Min(0f)] private float topOffset = 180f;

    [Header("Scale")]
    [SerializeField, Min(1f)] private float minRange = 5f;
    [SerializeField, Min(100f)] private float maxRange = 5000f;
    [SerializeField, Min(8f)] private float pixelsPer100m = 22f;
    [SerializeField, Min(5f)] private float minorTickStep = 25f;
    [SerializeField, Min(10f)] private float majorTickStep = 100f;

    [Header("Style")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color minorTickColor = new Color(1f, 1f, 1f, 0.65f);
    [SerializeField] private Color majorTickColor = new Color(1f, 0.95f, 0.45f, 0.95f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color centerMarkerColor = new Color(1f, 0.95f, 0.45f, 1f);

    [Header("Auto Fade")]
    [SerializeField] private bool useAutoFade = true;
    [SerializeField, Min(0f)] private float idleFadeDelay = 1.5f;
    [SerializeField, Min(0.01f)] private float fadeInSpeed = 8f;
    [SerializeField, Min(0.01f)] private float fadeOutSpeed = 2.5f;
    [SerializeField, Min(0f)] private float rangeActivityThresholdMeters = 0.25f;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0f;

    private RawImage tapeImage;
    private RectTransform tapeRect;
    private RectTransform viewportRect;
    private Text rangeText;
    private Texture2D generatedTapeTexture;
    private CanvasGroup rootCanvasGroup;
    private float lastObservedRange = float.NaN;
    private float lastActivityTime = -999f;

    private float pixelsPerMeter;

    private void Awake()
    {
        if (gunner == null)
            gunner = FindAnyObjectByType<GunnerController>();

        if (gunner != null)
        {
            minRange = gunner.MinRangeMeters;
            maxRange = gunner.MaxRangeMeters;
        }

        if (buildOnAwake)
            BuildRangeUI();
    }

    private void LateUpdate()
    {
        if (tapeRect == null || viewportRect == null)
            TryBindRuntimeRefs();

        if (gunner == null)
            gunner = FindAnyObjectByType<GunnerController>();

        if (tapeRect == null || viewportRect == null)
            return;

        float min = minRange;
        float max = maxRange;
        if (gunner != null)
        {
            min = gunner.MinRangeMeters;
            max = gunner.MaxRangeMeters;
        }

        min = Mathf.Min(min, max - 1f);
        max = Mathf.Max(min + 1f, max);

        float current = gunner != null ? gunner.CurrentRangeMeters : min;
        current = Mathf.Clamp(current, min, max);

        pixelsPerMeter = Mathf.Max(0.0001f, pixelsPer100m / 100f);

        float tapeY = (viewportRect.rect.height * 0.5f) - ((current - min) * pixelsPerMeter);
        Vector2 p = tapeRect.anchoredPosition;
        p.y = tapeY;
        tapeRect.anchoredPosition = p;

        if (rangeText != null)
            rangeText.text = $"{current:0} m";

        UpdateAutoFade(current);
    }

    [ContextMenu("Build Range Tape UI")]
    public void BuildRangeUI()
    {
        pixelsPerMeter = Mathf.Max(0.0001f, pixelsPer100m / 100f);

        RectTransform root = GetOrCreateRect(transform, "RangeBarRoot");
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-rightOffset, -topOffset);
        root.sizeDelta = new Vector2(tapeWidth + 30f, tapeHeight + 56f);

        RectTransform viewport = GetOrCreateRect(root, "Viewport");
        viewport.anchorMin = new Vector2(1f, 0.5f);
        viewport.anchorMax = new Vector2(1f, 0.5f);
        viewport.pivot = new Vector2(1f, 0.5f);
        viewport.anchoredPosition = Vector2.zero;
        viewport.sizeDelta = new Vector2(tapeWidth, tapeHeight);

        Image bg = GetOrAdd<Image>(viewport.gameObject);
        bg.color = backgroundColor;
        bg.raycastTarget = false;

        RectMask2D mask = viewport.GetComponent<RectMask2D>();
        if (mask == null) viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform strip = GetOrCreateRect(viewport, "TapeStrip");
        strip.anchorMin = new Vector2(0f, 0f);
        strip.anchorMax = new Vector2(0f, 0f);
        strip.pivot = new Vector2(0f, 0f);
        strip.anchoredPosition = Vector2.zero;

        int texWidth = Mathf.RoundToInt(tapeWidth);
        int texHeight = Mathf.Max(Mathf.RoundToInt((maxRange - minRange) * pixelsPerMeter) + 2, Mathf.RoundToInt(tapeHeight));
        Texture2D tapeTex = CreateOrUpdateTexture(texWidth, texHeight);

        RawImage tape = GetOrAdd<RawImage>(strip.gameObject);
        tape.texture = tapeTex;
        tape.color = Color.white;
        tape.raycastTarget = false;

        strip.sizeDelta = new Vector2(texWidth, texHeight);

        BuildLabels(strip, texWidth, texHeight);
        BuildCenterMarker(root, tapeWidth, tapeHeight);
        BuildRangeValue(root, tapeHeight);

        tapeImage = tape;
        tapeRect = strip;
        viewportRect = viewport;
        rootCanvasGroup = root.GetComponent<CanvasGroup>();
        if (rootCanvasGroup == null) rootCanvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
        rootCanvasGroup.alpha = useAutoFade ? hiddenAlpha : visibleAlpha;
        lastObservedRange = float.NaN;
        lastActivityTime = Time.unscaledTime;

        if (rangeText == null)
        {
            Transform valueTf = root.Find("Value");
            if (valueTf != null) rangeText = valueTf.GetComponent<Text>();
        }
    }

    private void OnDestroy()
    {
        if (generatedTapeTexture != null)
            Destroy(generatedTapeTexture);
    }

    private void TryBindRuntimeRefs()
    {
        Transform root = transform.Find("RangeBarRoot");
        if (root == null) return;

        Transform viewport = root.Find("Viewport");
        if (viewport == null) return;

        viewportRect = viewport as RectTransform;
        Transform strip = viewport.Find("TapeStrip");
        if (strip != null)
        {
            tapeRect = strip as RectTransform;
            tapeImage = strip.GetComponent<RawImage>();
        }

        Transform value = root.Find("Value");
        if (value != null)
            rangeText = value.GetComponent<Text>();

        rootCanvasGroup = root.GetComponent<CanvasGroup>();
    }

    private Texture2D CreateOrUpdateTexture(int width, int height)
    {
        width = Mathf.Max(32, width);
        height = Mathf.Max(32, height);

        if (generatedTapeTexture == null || generatedTapeTexture.width != width || generatedTapeTexture.height != height)
        {
            if (generatedTapeTexture != null) Destroy(generatedTapeTexture);
            generatedTapeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            generatedTapeTexture.name = "RangeTapeGenerated";
            generatedTapeTexture.wrapMode = TextureWrapMode.Clamp;
            generatedTapeTexture.filterMode = FilterMode.Bilinear;
        }

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        generatedTapeTexture.SetPixels(pixels);

        int spineX = Mathf.RoundToInt(width * 0.88f);
        DrawVerticalLine(generatedTapeTexture, spineX, 0, height, new Color(1f, 1f, 1f, 0.25f));

        float start = minRange;
        float end = maxRange;
        float firstTick = Mathf.Floor(start / minorTickStep) * minorTickStep;

        for (float r = firstTick; r <= end + minorTickStep; r += minorTickStep)
        {
            if (r < start - 0.001f) continue;
            int y = Mathf.RoundToInt((r - start) * pixelsPerMeter);
            bool major = Mathf.Abs(Mathf.Repeat(r, majorTickStep)) < 0.001f || Mathf.Abs(Mathf.Repeat(r, majorTickStep) - majorTickStep) < 0.001f;

            int tickLen = major ? Mathf.RoundToInt(width * 0.55f) : Mathf.RoundToInt(width * 0.30f);
            Color c = major ? majorTickColor : minorTickColor;
            DrawHorizontalLine(generatedTapeTexture, y, Mathf.Max(0, spineX - tickLen), spineX, c);
        }

        generatedTapeTexture.Apply(false, false);
        return generatedTapeTexture;
    }

    private void BuildLabels(RectTransform strip, int texWidth, int texHeight)
    {
        Transform old = strip.Find("Labels");
        if (old != null) DestroyImmediate(old.gameObject);

        RectTransform labels = GetOrCreateRect(strip, "Labels");
        labels.anchorMin = new Vector2(0f, 0f);
        labels.anchorMax = new Vector2(0f, 0f);
        labels.pivot = new Vector2(0f, 0f);
        labels.anchoredPosition = Vector2.zero;
        labels.sizeDelta = new Vector2(texWidth, texHeight);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        float firstLabel = Mathf.Ceil(minRange / majorTickStep) * majorTickStep;
        for (float r = firstLabel; r <= maxRange + 0.001f; r += majorTickStep)
        {
            int y = Mathf.RoundToInt((r - minRange) * pixelsPerMeter);

            GameObject go = new GameObject($"R_{r:0}", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(labels, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(texWidth * 0.28f, y);
            rect.sizeDelta = new Vector2(texWidth * 0.24f, 20f);

            Text txt = go.GetComponent<Text>();
            txt.text = $"{r:0}";
            txt.font = font;
            txt.fontSize = 14;
            txt.fontStyle = FontStyle.Normal;
            txt.alignment = TextAnchor.MiddleRight;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.color = textColor;
            txt.raycastTarget = false;

            Outline ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.7f);
            ol.effectDistance = new Vector2(1f, -1f);
        }
    }

    private void BuildCenterMarker(RectTransform root, float width, float height)
    {
        RectTransform marker = GetOrCreateRect(root, "CenterMarker");
        marker.anchorMin = new Vector2(1f, 0.5f);
        marker.anchorMax = new Vector2(1f, 0.5f);
        marker.pivot = new Vector2(1f, 0.5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(width, 2f);

        Image img = GetOrAdd<Image>(marker.gameObject);
        img.color = centerMarkerColor;
        img.raycastTarget = false;
    }

    private void BuildRangeValue(RectTransform root, float barHeight)
    {
        RectTransform value = GetOrCreateRect(root, "Value");
        value.anchorMin = new Vector2(1f, 0.5f);
        value.anchorMax = new Vector2(1f, 0.5f);
        value.pivot = new Vector2(1f, 1f);
        value.anchoredPosition = new Vector2(0f, -(barHeight * 0.5f) - 8f);
        value.sizeDelta = new Vector2(120f, 28f);

        Text txt = GetOrAdd<Text>(value.gameObject);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 20;
        txt.fontStyle = FontStyle.Bold;
        txt.color = textColor;
        txt.raycastTarget = false;
        txt.text = $"{minRange:0} m";

        rangeText = txt;
    }

    private static void DrawVerticalLine(Texture2D tex, int x, int yStart, int yLen, Color color)
    {
        x = Mathf.Clamp(x, 0, tex.width - 1);
        int y0 = Mathf.Clamp(yStart, 0, tex.height - 1);
        int y1 = Mathf.Clamp(yStart + yLen, 0, tex.height);
        for (int y = y0; y < y1; y++) tex.SetPixel(x, y, color);
    }

    private static void DrawHorizontalLine(Texture2D tex, int y, int xStart, int xEnd, Color color)
    {
        y = Mathf.Clamp(y, 0, tex.height - 1);
        int xs = Mathf.Clamp(xStart, 0, tex.width - 1);
        int xe = Mathf.Clamp(xEnd, 0, tex.width - 1);
        if (xe < xs)
        {
            int t = xs;
            xs = xe;
            xe = t;
        }

        for (int x = xs; x <= xe; x++) tex.SetPixel(x, y, color);
    }

    private static RectTransform GetOrCreateRect(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            child = go.transform;
            child.SetParent(parent, false);
        }

        RectTransform rect = child as RectTransform;
        if (rect == null) rect = child.gameObject.AddComponent<RectTransform>();
        return rect;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    private void UpdateAutoFade(float currentRange)
    {
        if (rootCanvasGroup == null) return;

        if (!useAutoFade)
        {
            rootCanvasGroup.alpha = visibleAlpha;
            return;
        }

        float now = Time.unscaledTime;
        bool active = false;

        if (float.IsNaN(lastObservedRange))
            active = true;
        else
            active = Mathf.Abs(currentRange - lastObservedRange) >= rangeActivityThresholdMeters;

        if (active)
            lastActivityTime = now;

        float targetAlpha = (now - lastActivityTime <= idleFadeDelay) ? visibleAlpha : hiddenAlpha;
        float speed = targetAlpha > rootCanvasGroup.alpha ? fadeInSpeed : fadeOutSpeed;
        rootCanvasGroup.alpha = Mathf.MoveTowards(rootCanvasGroup.alpha, targetAlpha, speed * Time.unscaledDeltaTime);
        lastObservedRange = currentRange;
    }
}
