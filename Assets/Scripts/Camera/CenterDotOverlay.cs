using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CameraController))]
public class CenterDotOverlay : MonoBehaviour
{
    [Header("Build")]
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private int sortingOrder = 9998;

    [Header("Dot")]
    [SerializeField, Min(1f)] private float dotSize = 6f;
    [SerializeField] private Color dotColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private bool hideWhileZooming = true;

    private CameraController cameraController;
    private GameObject overlayRoot;

    private void Awake()
    {
        cameraController = GetComponent<CameraController>();
        if (buildOnAwake) BuildUI();
    }

    private void OnEnable()
    {
        overlayRoot = FindOverlayRoot();
        if (overlayRoot == null)
            BuildUI();
    }

    private void Update()
    {
        if (cameraController == null) cameraController = GetComponent<CameraController>();
        if (overlayRoot == null) overlayRoot = FindOverlayRoot();
        if (overlayRoot == null) return;

        bool hide = hideWhileZooming && cameraController != null && cameraController.IsZooming;
        bool wantActive = !hide;
        if (overlayRoot.activeSelf != wantActive)
            overlayRoot.SetActive(wantActive);
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

        GameObject dotGroup = new GameObject("CenterDotGroup", typeof(RectTransform));
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

    private static void DestroyObject(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}
