using UnityEngine;

/// <summary>
/// Simple horizontal compass bar.
/// Place two identical strips (A/B) side by side and this script scrolls them by target yaw.
/// </summary>
public class CompassBar : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("UI Strips (same content)")]
    [SerializeField] private RectTransform stripA;
    [SerializeField] private RectTransform stripB;

    [Header("Tuning")]
    [SerializeField, Min(1f)] private float widthPer360 = 2048f;
    [SerializeField] private bool useLocalYaw = false;
    [SerializeField] private bool preferRootBodyTarget = true;

    public void Configure(Transform targetTransform, RectTransform a, RectTransform b, float stripWidth)
    {
        target = targetTransform;
        stripA = a;
        stripB = b;
        widthPer360 = Mathf.Max(1f, stripWidth);
    }

    public void SetUseLocalYaw(bool enabled)
    {
        useLocalYaw = enabled;
    }

    private void Reset()
    {
        TryResolveTarget();
    }

    private void LateUpdate()
    {
        if (stripA == null || stripB == null) return;
        if (target == null && !TryResolveTarget()) return;

        RectTransform viewport = stripA.parent as RectTransform;
        if (viewport == null) return;

        float yaw = GetYaw(target, useLocalYaw);
        float centerOffset = viewport.rect.width * 0.5f;
        // Keep stripA in [-width, 0) so left side is always covered (no seam gap near 0/360)
        float x = Mathf.Repeat(centerOffset - (yaw / 360f) * widthPer360, widthPer360) - widthPer360;

        Vector2 aPos = stripA.anchoredPosition;
        Vector2 bPos = stripB.anchoredPosition;
        aPos.x = x;
        bPos.x = x + widthPer360;

        stripA.anchoredPosition = aPos;
        stripB.anchoredPosition = bPos;
    }

    private bool TryResolveTarget()
    {
        if (target != null) return true;

        CameraController cameraController = FindAnyObjectByType<CameraController>();
        if (cameraController != null && cameraController.Cam != null)
        {
            target = ResolveStableTarget(cameraController.Cam.transform);
            return true;
        }

        if (Camera.main != null)
        {
            target = ResolveStableTarget(Camera.main.transform);
            return true;
        }

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

        if (best != null)
        {
            target = ResolveStableTarget(best.transform);
            return true;
        }

        return false;
    }

    private Transform ResolveStableTarget(Transform camTransform)
    {
        if (camTransform == null) return null;
        if (!preferRootBodyTarget) return camTransform;

        Rigidbody rb = camTransform.GetComponentInParent<Rigidbody>();
        if (rb != null) return rb.transform;

        return camTransform.root != null ? camTransform.root : camTransform;
    }

    private static float GetYaw(Transform t, bool local)
    {
        if (local)
        {
            float y = t.localEulerAngles.y;
            if (y < 0f) y += 360f;
            return y;
        }

        Vector3 flatForward = t.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.0001f)
            return t.eulerAngles.y;

        flatForward.Normalize();
        float yaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;
        if (yaw < 0f) yaw += 360f;
        return yaw;
    }
}
