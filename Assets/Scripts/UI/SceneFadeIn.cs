using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SceneFadeIn : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 1.0f;

    private void Awake()
    {
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        // 브리핑 씬에서 넘어온 오버레이가 있으면 재사용, 없으면 새로 생성
        var existing = GameObject.Find(BriefingSceneLoader.OverlayName);
        CanvasGroup cg;

        if (existing != null)
        {
            cg = existing.GetComponent<CanvasGroup>();
            if (cg == null) cg = existing.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
        }
        else
        {
            existing = BuildBlackCanvas();
            cg = existing.GetComponent<CanvasGroup>();
        }

        // 페이드아웃
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = 1f - Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }

        Destroy(existing);
    }

    private static GameObject BuildBlackCanvas()
    {
        var go = new GameObject(BriefingSceneLoader.OverlayName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

        var canvas          = go.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var scaler                 = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var cg            = go.AddComponent<CanvasGroup>();
        cg.alpha          = 1f;
        cg.blocksRaycasts = false;

        var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        var r = img.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        img.GetComponent<Image>().color = Color.black;

        return go;
    }
}
