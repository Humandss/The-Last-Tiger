using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera commanderCam;

    [Header("Zoom")]
    [SerializeField] private KeyCode zoomHoldKey = KeyCode.Mouse1;
    [SerializeField] private bool wheelOnlyWhileZooming = true;
    [SerializeField] private float[] zoomMagnifications = new float[] { 4f, 6f, 8f };
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float zoomFovSmooth = 16f;
    [SerializeField] private float minFovClamp = 5f;
    [SerializeField] private float maxFovClamp = 90f;
    [SerializeField] private bool lockCursorWhileZooming = true;
    [SerializeField] private bool hideCursorWhileZooming = true;
    private bool isZooming;
    private int zoomIndex = 1;
    private float targetFov;

    [Header("Sensitivity")]
    [SerializeField] private bool scaleSensitivityByFov = true;
    [SerializeField] private float zoomSensMinMul = 0.12f;
    [SerializeField] private float zoomSensExponent = 0.85f;

    [Header("Shake")]
    [SerializeField] private Vector3 maxKickEuler = new Vector3(3f, 3f, 3f);
    [SerializeField] private bool randomizeYawRoll = true;
    [SerializeField] private float kickInSpeed = 40f;
    [SerializeField] private float returnSpeed = 20f;
    [SerializeField] private float damping = 16f;
    [SerializeField] private float noiseFreq = 22f;
    [SerializeField] private float noiseAmount = 0.20f;
    [SerializeField] private bool affectPitch = true;
    [SerializeField] private bool affectYaw = true;
    [SerializeField] private bool affectRoll = true;

    [Header("Hit Shake")]
    [SerializeField] private Vector3 maxHitKickEuler = new Vector3(8f, 6f, 4f);
    [SerializeField] private float hitKickInSpeed = 25f;
    [SerializeField] private float hitReturnSpeed = 4f;

    private Quaternion _baseLocalRot;
    private Vector3 _curEuler;
    private Vector3 _targetEuler;
    private float _noiseSeed;
    private bool _returning;
    private float _curKickInSpeed;
    private float _curReturnSpeed;

    private CursorLockMode _prevCursorLock;
    private bool _prevCursorVisible;
    private bool _cursorStateCaptured;

    private void Awake()
    {
        if (!commanderCam) commanderCam = GetComponentInChildren<Camera>();

        if (commanderCam != null)
        {
            baseFov = commanderCam.fieldOfView;
            targetFov = baseFov;
        }

        zoomIndex = Mathf.Clamp(zoomIndex, 0, zoomMagnifications.Length - 1);
        _baseLocalRot = transform.localRotation;
        _noiseSeed = Random.value * 1000f;
        _curKickInSpeed = kickInSpeed;
        _curReturnSpeed = returnSpeed;
    }

    private void OnEnable()
    {
        _baseLocalRot = transform.localRotation;
    }

    private void OnDisable()
    {
        RestoreCursorState();
    }

    private void Update()
    {
        HandleZoomInput();
        UpdateZoomFov(Time.deltaTime);

        // Some systems can unlock cursor; keep it centered while zooming.
        if (isZooming)
            ApplyCursorForZoom(true);
    }

    private void LateUpdate()
    {
        UpdateShake(Time.deltaTime);
    }

    private void HandleZoomInput()
    {
        if (Input.GetKeyDown(zoomHoldKey))
        {
            isZooming = !isZooming;
            ApplyCursorForZoom(isZooming);
        }

        bool canWheel = !wheelOnlyWhileZooming || isZooming;
        if (canWheel)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f)
                zoomIndex = Mathf.Clamp(zoomIndex + 1, 0, zoomMagnifications.Length - 1);
            else if (wheel < -0.01f)
                zoomIndex = Mathf.Clamp(zoomIndex - 1, 0, zoomMagnifications.Length - 1);
        }

        targetFov = isZooming
            ? Mathf.Clamp(baseFov / Mathf.Max(1f, GetCurrentZoomMag()), minFovClamp, maxFovClamp)
            : Mathf.Clamp(baseFov, minFovClamp, maxFovClamp);
    }

    private void ApplyCursorForZoom(bool zoomOn)
    {
        if (!lockCursorWhileZooming && !hideCursorWhileZooming) return;

        if (zoomOn)
        {
            if (!_cursorStateCaptured)
            {
                _prevCursorLock = Cursor.lockState;
                _prevCursorVisible = Cursor.visible;
                _cursorStateCaptured = true;
            }

            if (lockCursorWhileZooming)
                Cursor.lockState = CursorLockMode.Locked;
            if (hideCursorWhileZooming)
                Cursor.visible = false;
        }
        else
        {
            RestoreCursorState();
        }
    }

    private void RestoreCursorState()
    {
        if (!_cursorStateCaptured) return;

        Cursor.lockState = _prevCursorLock;
        Cursor.visible = _prevCursorVisible;
        _cursorStateCaptured = false;
    }

    private void UpdateZoomFov(float dt)
    {
        if (commanderCam == null) return;
        float a = 1f - Mathf.Exp(-Mathf.Max(0.01f, zoomFovSmooth) * dt);
        commanderCam.fieldOfView = Mathf.Lerp(commanderCam.fieldOfView, targetFov, a);
    }

    private float GetCurrentZoomMag()
    {
        if (zoomMagnifications == null || zoomMagnifications.Length == 0) return 1f;
        return Mathf.Max(1f, zoomMagnifications[Mathf.Clamp(zoomIndex, 0, zoomMagnifications.Length - 1)]);
    }

    private float GetMaxZoomMag()
    {
        if (zoomMagnifications == null || zoomMagnifications.Length == 0) return 1f;
        float max = 1f;
        for (int i = 0; i < zoomMagnifications.Length; i++)
            max = Mathf.Max(max, zoomMagnifications[i]);
        return max;
    }

    public float GetSensitivityMultiplier()
    {
        if (!scaleSensitivityByFov || commanderCam == null) return 1f;
        float ratio = Mathf.Clamp01(commanderCam.fieldOfView / Mathf.Max(1f, baseFov));
        float scaled = Mathf.Pow(Mathf.Max(0.0001f, ratio), Mathf.Max(0.01f, zoomSensExponent));
        return Mathf.Clamp(scaled, zoomSensMinMul, 1f);
    }

    public void TriggerShake(float intensity = 1f)
    {
        intensity = Mathf.Max(0f, intensity);

        float yawSign = randomizeYawRoll ? (Random.value < 0.5f ? -1f : 1f) : 1f;
        float rollSign = randomizeYawRoll ? (Random.value < 0.5f ? -1f : 1f) : 1f;

        Vector3 kick = new Vector3(
            maxKickEuler.x * intensity,
            maxKickEuler.y * intensity * yawSign * Random.Range(0.6f, 1f),
            maxKickEuler.z * intensity * rollSign * Random.Range(0.4f, 1f)
        );

        _curEuler += kick * 0.35f;
        _curEuler = ClampPerAxis(_curEuler, maxKickEuler * 2.5f);
        _targetEuler = kick;
        _returning = false;
    }

    public void TriggerHitShake(Vector3 hitDirection, float intensity = 1f)
    {
        float yawSign = Random.value < 0.5f ? -1f : 1f;
        float rollSign = Random.value < 0.5f ? -1f : 1f;

        Vector3 kick = new Vector3(
            maxHitKickEuler.x * intensity,
            maxHitKickEuler.y * intensity * yawSign * Random.Range(0.5f, 1f),
            maxHitKickEuler.z * intensity * rollSign * Random.Range(0.3f, 1f)
        );

        _curEuler = kick;
        _curEuler = ClampPerAxis(_curEuler, maxHitKickEuler * 3f);
        _targetEuler = kick;
        _curKickInSpeed = hitKickInSpeed;
        _curReturnSpeed = hitReturnSpeed;
        _returning = false;
    }

    private void UpdateShake(float dt)
    {
        float speed = _returning ? _curReturnSpeed : _curKickInSpeed;
        _curEuler = Vector3.MoveTowards(_curEuler, _targetEuler, speed * dt);

        if (!_returning && (_curEuler - _targetEuler).sqrMagnitude <= 0.0001f)
        {
            _returning = true;
            _targetEuler = Vector3.zero;
        }

        Vector3 noise = Vector3.zero;
        if (_returning && _curEuler.sqrMagnitude > 0.00001f && noiseAmount > 0f)
        {
            float t = Time.time * noiseFreq + _noiseSeed;
            float nx = Mathf.PerlinNoise(t, 0.13f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0.37f, t) * 2f - 1f;
            float nz = Mathf.PerlinNoise(t, 0.71f) * 2f - 1f;
            float amp = _curEuler.magnitude * noiseAmount;
            noise = new Vector3(nx, ny, nz) * amp * Mathf.Exp(-damping * dt);
        }

        Vector3 final = _curEuler + noise;
        if (!affectPitch) final.x = 0f;
        if (!affectYaw) final.y = 0f;
        if (!affectRoll) final.z = 0f;

        transform.localRotation = _baseLocalRot * Quaternion.Euler(final);
    }

    private static Vector3 ClampPerAxis(Vector3 v, Vector3 maxAbs)
    {
        v.x = Mathf.Clamp(v.x, -Mathf.Abs(maxAbs.x), Mathf.Abs(maxAbs.x));
        v.y = Mathf.Clamp(v.y, -Mathf.Abs(maxAbs.y), Mathf.Abs(maxAbs.y));
        v.z = Mathf.Clamp(v.z, -Mathf.Abs(maxAbs.z), Mathf.Abs(maxAbs.z));
        return v;
    }

    public bool IsZooming => isZooming;
    public Camera Cam => commanderCam;

    // 0: no zoom, 1: strongest zoom stage
    public float ZoomAmount01
    {
        get
        {
            if (!isZooming || commanderCam == null) return 0f;
            float maxMag = GetMaxZoomMag();
            float curMag = Mathf.Max(1f, baseFov / Mathf.Max(0.001f, commanderCam.fieldOfView));
            return Mathf.Clamp01(Mathf.InverseLerp(1f, maxMag, curMag));
        }
    }
}
