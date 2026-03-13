using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class CompassBarAutoSetup : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool preferRootBodyTarget = true;

    [Header("Build")]
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField, Min(24f)] private float barHeight = 48f;
    [SerializeField, Min(300f)] private float barWidth = 760f;
    [SerializeField, Min(0f)] private float topOffset = 8f;
    [SerializeField, Min(256f)] private float widthPer360 = 1440f;

    [Header("Style")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.3f);
    [SerializeField] private Color tickColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color degreeTextColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color cardinalTextColor = Color.white;
    [SerializeField] private bool createCenterMarker = true;

    [Header("Auto Fade")]
    [SerializeField] private bool useAutoFade = true;
    [SerializeField, Min(0f)] private float idleFadeDelay = 1.5f;
    [SerializeField, Min(0.01f)] private float fadeInSpeed = 8f;
    [SerializeField, Min(0.01f)] private float fadeOutSpeed = 2.5f;
    [SerializeField, Min(0f)] private float yawActivityThresholdDeg = 0.12f;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0f;

    private Texture2D generatedTexture;
    private CompassBar runtimeBar;
    private RectTransform runtimeStripA;
    private RectTransform runtimeStripB;
    private CanvasGroup runtimeCanvasGroup;
    private float lastObservedYaw = float.NaN;
    private float lastActivityTime = -999f;

    private void Awake()
    {
        if (buildOnAwake)
            BuildCompassUI();
    }

    private void LateUpdate()
    {
        if (runtimeBar == null || runtimeStripA == null || runtimeStripB == null)
            TryBindRuntimeRefs();

        if (runtimeBar == null || runtimeStripA == null || runtimeStripB == null)
            return;

        Transform t = target != null ? target : ResolveTarget();
        if (t == null) return;

        runtimeBar.Configure(t, runtimeStripA, runtimeStripB, widthPer360);
        runtimeBar.SetUseLocalYaw(false);

        UpdateAutoFade(t);
    }

    [ContextMenu("Build Compass UI")]
    public void BuildCompassUI()
    {
        Transform canvasTf = transform;

        RectTransform root = GetOrCreateRect(canvasTf, "CompassRoot");
        AnchorTopCenter(root, barWidth, barHeight, topOffset);

        RectTransform viewport = GetOrCreateRect(root, "Viewport");
        StretchFull(viewport);
        EnsureMask(viewport);

        RectTransform stripA = GetOrCreateRect(viewport, "StripA");
        RectTransform stripB = GetOrCreateRect(viewport, "StripB");
        SetupStripRect(stripA, 0f, widthPer360, barHeight);
        SetupStripRect(stripB, widthPer360, widthPer360, barHeight);

        Texture2D texture = CreateOrUpdateTexture(Mathf.RoundToInt(widthPer360), Mathf.RoundToInt(barHeight));
        EnsureStripImage(stripA, texture);
        EnsureStripImage(stripB, texture);

        BuildLabels(stripA, barHeight, widthPer360);
        BuildLabels(stripB, barHeight, widthPer360);

        if (createCenterMarker)
            EnsureCenterMarker(root, barHeight);

        CompassBar bar = root.GetComponent<CompassBar>();
        if (bar == null) bar = root.gameObject.AddComponent<CompassBar>();

        Transform targetTransform = target != null ? target : ResolveTarget();
        target = targetTransform;

        bar.Configure(targetTransform, stripA, stripB, widthPer360);
        bar.SetUseLocalYaw(false);

        runtimeBar = bar;
        runtimeStripA = stripA;
        runtimeStripB = stripB;
        runtimeCanvasGroup = root.GetComponent<CanvasGroup>();
        if (runtimeCanvasGroup == null) runtimeCanvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        runtimeCanvasGroup.interactable = false;
        runtimeCanvasGroup.blocksRaycasts = false;
        runtimeCanvasGroup.alpha = useAutoFade ? hiddenAlpha : visibleAlpha;
        lastObservedYaw = float.NaN;
        lastActivityTime = Time.unscaledTime;
    }

    private void OnDestroy()
    {
        if (generatedTexture != null)
            Destroy(generatedTexture);
    }

    private void TryBindRuntimeRefs()
    {
        Transform root = transform.Find("CompassRoot");
        if (root == null) return;

        runtimeBar = root.GetComponent<CompassBar>();
        runtimeCanvasGroup = root.GetComponent<CanvasGroup>();

        Transform viewport = root.Find("Viewport");
        if (viewport == null) return;

        runtimeStripA = viewport.Find("StripA") as RectTransform;
        runtimeStripB = viewport.Find("StripB") as RectTransform;
    }

    private void UpdateAutoFade(Transform t)
    {
        if (runtimeCanvasGroup == null) return;

        if (!useAutoFade)
        {
            runtimeCanvasGroup.alpha = visibleAlpha;
            return;
        }

        float now = Time.unscaledTime;
        float yaw = GetWorldYaw(t);

        bool active = false;
        if (float.IsNaN(lastObservedYaw))
        {
            active = true;
        }
        else
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(lastObservedYaw, yaw));
            active = d >= yawActivityThresholdDeg;
        }

        if (active)
            lastActivityTime = now;

        float targetAlpha = (now - lastActivityTime <= idleFadeDelay) ? visibleAlpha : hiddenAlpha;
        float speed = targetAlpha > runtimeCanvasGroup.alpha ? fadeInSpeed : fadeOutSpeed;
        runtimeCanvasGroup.alpha = Mathf.MoveTowards(runtimeCanvasGroup.alpha, targetAlpha, speed * Time.unscaledDeltaTime);
        lastObservedYaw = yaw;
    }

    private static float GetWorldYaw(Transform t)
    {
        if (t == null) return 0f;
        Vector3 flatForward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f) return t.eulerAngles.y;
        flatForward.Normalize();
        float yaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;
        if (yaw < 0f) yaw += 360f;
        return yaw;
    }

    private RectTransform GetOrCreateRect(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            child = go.transform;
            child.SetParent(parent, false);
        }

        RectTransform rect = child.GetComponent<RectTransform>();
        if (rect == null) rect = child.gameObject.AddComponent<RectTransform>();
        return rect;
    }

    private void AnchorTopCenter(RectTransform rect, float width, float height, float yOffset)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -yOffset);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private void SetupStripRect(RectTransform rect, float x, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void EnsureMask(RectTransform viewport)
    {
        Image image = viewport.GetComponent<Image>();
        if (image == null) image = viewport.gameObject.AddComponent<Image>();
        image.color = backgroundColor;

        RectMask2D mask = viewport.GetComponent<RectMask2D>();
        if (mask == null) viewport.gameObject.AddComponent<RectMask2D>();
    }

    private void EnsureStripImage(RectTransform strip, Texture2D texture)
    {
        RawImage image = strip.GetComponent<RawImage>();
        if (image == null) image = strip.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
    }

    private void BuildLabels(RectTransform strip, float height, float width)
    {
        Transform existing = strip.Find("Labels");
        if (existing != null) DestroyImmediate(existing.gameObject);

        RectTransform labelsRoot = GetOrCreateRect(strip, "Labels");
        StretchFull(labelsRoot);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        for (int deg = 0; deg < 360; deg += 15)
        {
            float x = (deg / 360f) * width;

            if (deg % 90 == 0)
            {
                string dir = GetCardinal(deg);
                CreateLabel(labelsRoot, font, $"Cardinal_{dir}", dir, x, height * 0.50f, 34f, 30, FontStyle.Bold, cardinalTextColor);
            }
            else
            {
                CreateLabel(labelsRoot, font, $"Deg_{deg}", deg.ToString(), x, height * 0.20f, 48f, 16, FontStyle.Normal, degreeTextColor);
            }
        }

        // Duplicate 0/360 label at strip end so N is not clipped on wrap seam.
        CreateLabel(labelsRoot, font, "Cardinal_N_360", "N", width, height * 0.50f, 34f, 30, FontStyle.Bold, cardinalTextColor);
    }

    private void CreateLabel(RectTransform parent, Font font, string name, string textValue, float x, float y, float width, int fontSize, FontStyle style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, fontSize + 8f);

        Text txt = go.GetComponent<Text>();
        txt.text = textValue;
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.color = color;

        Outline outline = go.GetComponent<Outline>();
        if (outline == null) outline = go.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private static string GetCardinal(int deg)
    {
        switch (deg)
        {
            case 0: return "N";
            case 90: return "E";
            case 180: return "S";
            case 270: return "W";
            default: return "";
        }
    }

    private void EnsureCenterMarker(RectTransform root, float height)
    {
        RectTransform marker = GetOrCreateRect(root, "CenterMarker");
        marker.anchorMin = new Vector2(0.5f, 1f);
        marker.anchorMax = new Vector2(0.5f, 1f);
        marker.pivot = new Vector2(0.5f, 1f);
        marker.anchoredPosition = new Vector2(0f, 0f);
        marker.sizeDelta = new Vector2(14f, 12f);

        Image image = marker.GetComponent<Image>();
        if (image == null) image = marker.gameObject.AddComponent<Image>();
        image.color = new Color(0.95f, 0.95f, 0.95f, 0.95f);
    }

    private Texture2D CreateOrUpdateTexture(int width, int height)
    {
        width = Mathf.Max(256, width);
        height = Mathf.Max(24, height);

        if (generatedTexture == null || generatedTexture.width != width || generatedTexture.height != height)
        {
            if (generatedTexture != null) Destroy(generatedTexture);
            generatedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            generatedTexture.name = "CompassBarGenerated";
            generatedTexture.wrapMode = TextureWrapMode.Clamp;
            generatedTexture.filterMode = FilterMode.Bilinear;
        }

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        generatedTexture.SetPixels(pixels);

        DrawHorizontalLine(generatedTexture, Mathf.RoundToInt(height * 0.2f), new Color(1f, 1f, 1f, 0.2f));

        for (int deg = 0; deg <= 360; deg += 5)
        {
            int x = Mathf.RoundToInt((deg / 360f) * (width - 1));

            float scale = 0.2f;
            if (deg % 90 == 0) scale = 0.65f;
            else if (deg % 45 == 0) scale = 0.5f;
            else if (deg % 15 == 0) scale = 0.35f;

            int tickHeight = Mathf.Max(3, Mathf.RoundToInt(height * scale));
            Color c = (deg % 90 == 0) ? new Color(1f, 0.95f, 0.45f, 1f) : tickColor;
            DrawVerticalLine(generatedTexture, x, 0, tickHeight, c);
        }

        generatedTexture.Apply(false, false);
        return generatedTexture;
    }

    private void DrawHorizontalLine(Texture2D tex, int y, Color color)
    {
        y = Mathf.Clamp(y, 0, tex.height - 1);
        for (int x = 0; x < tex.width; x++) tex.SetPixel(x, y, color);
    }

    private void DrawVerticalLine(Texture2D tex, int x, int yStart, int yLen, Color color)
    {
        x = Mathf.Clamp(x, 0, tex.width - 1);
        int yEnd = Mathf.Min(tex.height, yStart + yLen);
        for (int y = Mathf.Max(0, yStart); y < yEnd; y++) tex.SetPixel(x, y, color);
    }

    private Transform ResolveTarget()
    {
        if (target != null) return target;

        CameraController cameraController = FindAnyObjectByType<CameraController>();
        if (cameraController != null && cameraController.Cam != null)
            return ResolveStableTarget(cameraController.Cam.transform);

        if (Camera.main != null)
            return ResolveStableTarget(Camera.main.transform);

        Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Camera best = null;
        float bestDepth = float.NegativeInfinity;

        for (int i = 0; i < cams.Length; i++)
        {
            Camera cam = cams[i];
            if (cam == null || !cam.enabled) continue;

            if (cam.depth > bestDepth)
            {
                bestDepth = cam.depth;
                best = cam;
            }
        }

        return best != null ? ResolveStableTarget(best.transform) : null;
    }

    private Transform ResolveStableTarget(Transform camTransform)
    {
        if (camTransform == null) return null;
        if (!preferRootBodyTarget) return camTransform;

        Rigidbody rb = camTransform.GetComponentInParent<Rigidbody>();
        if (rb != null) return rb.transform;

        return camTransform.root != null ? camTransform.root : camTransform;
    }
}

