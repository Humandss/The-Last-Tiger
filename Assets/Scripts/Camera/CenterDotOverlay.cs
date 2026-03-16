using UnityEngine;
using UnityEngine.UI;

public class CenterDotOverlay : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CameraController cameraController;

    [Header("Build")]
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private int sortingOrder = 9998;

    [Header("Dot")]
    [SerializeField, Min(1f)] private float dotSize = 6f;
    [SerializeField] private Color dotColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private bool hideWhileZooming = true;

    [Header("Reload Time")]
    [SerializeField] private LoaderController loaderController;
    [SerializeField] private Color reloadTextColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField, Min(12)] private int reloadFontSize = 20;
    [SerializeField, Min(0f)] private float reloadTextOffsetY = 52f;

    private GameObject overlayRoot;
    private GameObject dotGroup;
    private Text reloadText;
    private Font reloadFont;

    private void Awake()
    {
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>(true);
        if (loaderController == null)
            loaderController = FindNearestPlayerLoader();
        if (buildOnAwake) BuildUI();
    }

    private void OnEnable()
    {
        overlayRoot = FindOverlayRoot();
        if (overlayRoot == null)
            BuildUI();
        else
            ResolveUiRefs();
    }

    private void Update()
    {
        if (cameraController == null) cameraController = FindObjectOfType<CameraController>(true);
        if (overlayRoot == null) overlayRoot = FindOverlayRoot();
        if (overlayRoot == null) return;
        if (dotGroup == null || reloadText == null) ResolveUiRefs();

        bool hide = hideWhileZooming && cameraController != null && cameraController.IsZooming;
        if (dotGroup != null && dotGroup.activeSelf == hide)
            dotGroup.SetActive(!hide);

        UpdateReloadTimeText();
    }

    [ContextMenu("Build Center Dot UI")]
    public void BuildUI()
    {
        Transform old = FindOverlayRootTransform();
        if (old != null) DestroyObject(old.gameObject);

        GameObject root = new GameObject("CenterDotOverlayUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        overlayRoot = root;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        dotGroup = new GameObject("CenterDotGroup", typeof(RectTransform));
        dotGroup.transform.SetParent(root.transform, false);

        RectTransform dotGroupRect = dotGroup.GetComponent<RectTransform>();
        dotGroupRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotGroupRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotGroupRect.pivot = new Vector2(0.5f, 0.5f);
        dotGroupRect.anchoredPosition = Vector2.zero;
        dotGroupRect.sizeDelta = Vector2.zero;

        GameObject dot = new GameObject("CenterDot", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(dotGroup.transform, false);

        RectTransform dotRect = dot.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = new Vector2(dotSize, dotSize);

        Image dotImage = dot.GetComponent<Image>();
        dotImage.color = dotColor;
        dotImage.raycastTarget = false;

        BuildReloadTimeText(root.transform);
    }

    private Transform FindOverlayRootTransform()
    {
        return transform.Find("CenterDotOverlayUI");
    }

    private GameObject FindOverlayRoot()
    {
        Transform t = FindOverlayRootTransform();
        return t != null ? t.gameObject : null;
    }

    private void BuildReloadTimeText(Transform parent)
    {
        reloadFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject textObject = new GameObject("ReloadTimeText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, -reloadTextOffsetY);
        textRect.sizeDelta = new Vector2(180f, 36f);

        reloadText = textObject.GetComponent<Text>();
        reloadText.font = reloadFont;
        reloadText.fontSize = reloadFontSize;
        reloadText.alignment = TextAnchor.MiddleCenter;
        reloadText.color = reloadTextColor;
        reloadText.raycastTarget = false;
        reloadText.text = string.Empty;
        textObject.SetActive(false);
    }

    private void ResolveUiRefs()
    {
        Transform dotGroupTransform = transform.Find("CenterDotOverlayUI/CenterDotGroup");
        if (dotGroupTransform != null)
            dotGroup = dotGroupTransform.gameObject;

        Transform reloadTextTransform = transform.Find("CenterDotOverlayUI/ReloadTimeText");
        if (reloadTextTransform != null)
            reloadText = reloadTextTransform.GetComponent<Text>();
    }

    private void UpdateReloadTimeText()
    {
        if (reloadText == null) return;
        if (loaderController == null) loaderController = FindNearestPlayerLoader();

        bool isLoading = loaderController != null && loaderController.GetIsLoading();
        if (!isLoading)
        {
            if (reloadText.gameObject.activeSelf)
                reloadText.gameObject.SetActive(false);
            return;
        }

        float remaining = loaderController.GetReloadSecondsRemaining();
        reloadText.text = $"{remaining:0.0}s";
        if (!reloadText.gameObject.activeSelf)
            reloadText.gameObject.SetActive(true);
    }

    private LoaderController FindNearestPlayerLoader()
    {
        LoaderController[] loaders = FindObjectsOfType<LoaderController>(true);
        LoaderController best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < loaders.Length; i++)
        {
            LoaderController candidate = loaders[i];
            if (candidate == null || candidate.IsAI) continue;

            float dist = (candidate.transform.position - transform.position).sqrMagnitude;
            if (dist < bestDist)
            {
                best = candidate;
                bestDist = dist;
            }
        }

        return best;
    }

    private static void DestroyObject(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}
