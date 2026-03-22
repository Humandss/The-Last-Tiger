using UnityEngine;
using UnityEngine.UI;

public class ZoomScopeOverlay : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CameraController cameraController;

    [Header("Build")]
    [SerializeField] private int sortingOrder = -100;

    [Header("Fade")]
    [SerializeField] private float fadeSpeed = 10f;
    [SerializeField, Range(0f, 1f)] private float maxOverlayAlpha = 0.70f;

    [Header("Binocular Fade")]
    [SerializeField, Range(0.30f, 1.20f)] private float lensSizeRatio = 1.10f;
    [SerializeField, Range(0.00f, 0.25f)] private float lensGapRatio = 0.01f;
    [SerializeField, Range(0.05f, 0.42f)] private float lensSeparationRatio = 0.20f;

    [Header("Reticle")]
    [SerializeField] private Color crosshairColor = new Color(1f, 1f, 1f, 0.75f);

    private CanvasGroup canvasGroup;
    private RawImage vignetteImage;
    private RectTransform crosshairRoot;
    private Texture2D crosshairTexture;
    private int lastW;
    private int lastH;

    private void Awake()
    {
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>(true);
        BuildUI();
        RefreshLayout(true);
    }

    private void OnEnable()
    {
        // Rebuild to avoid stale hierarchy when domain reload is disabled.
        BuildUI();
        RefreshLayout(true);
    }

    private void Update()
    {
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>(true);
        if (cameraController == null || canvasGroup == null) return;

        float t = cameraController.IsZooming ? Mathf.Lerp(0.35f, 1f, cameraController.ZoomAmount01) : 0f;
        float targetAlpha = t * maxOverlayAlpha;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        RefreshLayout(false);
    }

    private void BuildUI()
    {
        Transform existing = transform.Find("ZoomScopeOverlayUI");
        if (existing != null) Destroy(existing.gameObject);

        GameObject root = new GameObject("ZoomScopeOverlayUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject vignette = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage));
        vignette.transform.SetParent(root.transform, false);
        RectTransform vRect = vignette.GetComponent<RectTransform>();
        vRect.anchorMin = Vector2.zero;
        vRect.anchorMax = Vector2.one;
        vRect.offsetMin = Vector2.zero;
        vRect.offsetMax = Vector2.zero;

        vignetteImage = vignette.GetComponent<RawImage>();
        vignetteImage.color = Color.white;
        vignetteImage.raycastTarget = false;

        crosshairRoot = CreateCrosshairRoot(root.transform);
    }

    private void RefreshLayout(bool force)
    {
        int w = Screen.width;
        int h = Screen.height;
        if (!force && w == lastW && h == lastH) return;

        lastW = w;
        lastH = h;

        float shortSide = Mathf.Min(w, h);
        float lensSize = shortSide * lensSizeRatio;
        float lensRadius = lensSize * 0.5f;

        float gap = shortSide * lensGapRatio;
        float radiusX = lensRadius * 1.22f;
        float radiusY = lensRadius * 1.02f;
        float sepByRatio = shortSide * lensSeparationRatio;
        float sep = Mathf.Max(radiusX * 0.75f, sepByRatio);
        float softN = Mathf.Clamp(0.07f + (gap / Mathf.Max(1f, shortSide)) * 0.3f, 0.05f, 0.14f);

        if (vignetteImage != null)
            vignetteImage.texture = BuildBinocularVignetteTexture(w, h, radiusX, radiusY, sep, softN);

        if (crosshairRoot != null)
            crosshairRoot.anchoredPosition = Vector2.zero;
    }

    private RectTransform CreateCrosshairRoot(Transform parent)
    {
        GameObject root = new GameObject("CenterCrosshair", typeof(RectTransform), typeof(RawImage));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(64f, 64f);

        RawImage img = root.GetComponent<RawImage>();
        crosshairTexture = BuildCrosshairTexture(64, 64, 2, crosshairColor);
        img.texture = crosshairTexture;
        img.color = Color.white;
        img.raycastTarget = false;
        return rect;
    }

    private static Texture2D BuildCrosshairTexture(int width, int height, int thickness, Color color)
    {
        width = Mathf.Max(8, width);
        height = Mathf.Max(8, height);
        thickness = Mathf.Max(1, thickness);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);

        int cx = width / 2;
        int cy = height / 2;
        int halfT = thickness / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool onVertical = Mathf.Abs(x - cx) <= halfT;
                bool onHorizontal = Mathf.Abs(y - cy) <= halfT;
                if (onVertical || onHorizontal)
                    tex.SetPixel(x, y, color);
            }
        }

        tex.Apply(false, false);
        return tex;
    }

    private static Texture2D BuildBinocularVignetteTexture(int width, int height, float radiusX, float radiusY, float sepPx, float softN)
    {
        width = Mathf.Clamp(width, 256, 2048);
        height = Mathf.Clamp(height, 256, 2048);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;

        Vector2 left = new Vector2(cx - sepPx, cy);
        Vector2 right = new Vector2(cx + sepPx, cy);

        float fadeStart = 1f;
        float fadeEnd = 1f + Mathf.Max(0.01f, softN);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dxL = (x - left.x) / Mathf.Max(1f, radiusX);
                float dyL = (y - left.y) / Mathf.Max(1f, radiusY);
                float dL = Mathf.Sqrt(dxL * dxL + dyL * dyL);

                float dxR = (x - right.x) / Mathf.Max(1f, radiusX);
                float dyR = (y - right.y) / Mathf.Max(1f, radiusY);
                float dR = Mathf.Sqrt(dxR * dxR + dyR * dyR);

                float holeL = (dL <= fadeStart) ? 1f : (1f - Mathf.SmoothStep(fadeStart, fadeEnd, dL));
                float holeR = (dR <= fadeStart) ? 1f : (1f - Mathf.SmoothStep(fadeStart, fadeEnd, dR));
                float hole = Mathf.Max(holeL, holeR);

                tex.SetPixel(x, y, new Color(0f, 0f, 0f, 1f - hole));
            }
        }

        tex.Apply(false, false);
        return tex;
    }

    private void OnDestroy()
    {
        if (crosshairTexture != null)
            Destroy(crosshairTexture);
    }
}
