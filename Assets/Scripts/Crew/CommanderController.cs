using UnityEngine;

public enum ViewMode
{
    TurretLinked,
    FreeLook
}

public class CommanderController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform yawPivot;
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private CameraController cameraController; 

    [Header("Look")]
    [SerializeField] private float sensitivity = 170f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-20f, 60f);

    [Header("Input")]
    [SerializeField] private bool enableToggleKey = true;
    [SerializeField] private KeyCode freeLookToggleKey = KeyCode.V;
    [SerializeField] private bool requireRightMouse = false;

    [Header("State")]
    [SerializeField] private ViewMode mode = ViewMode.TurretLinked;

    private float yawLocal;
    private float yawWorld;
    private float pitch;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    private void Awake()
    {
        if (!yawPivot) yawPivot = transform;
        if (!pitchPivot) pitchPivot = transform;

        yawLocal = NormalizeAngle(yawPivot.localEulerAngles.y);
        yawWorld = yawPivot.eulerAngles.y;
        pitch = Mathf.Clamp(NormalizeAngle(pitchPivot.localEulerAngles.x), pitchLimits.x, pitchLimits.y);
    }

    private void Update()
    {
        HandleViewModeInput();

        if (requireRightMouse && !Input.GetMouseButton(1)) return;

        float dt = Time.deltaTime;
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");

        // 감도 배율은 CameraController에서 가져옴
        float sensMul = cameraController != null ? cameraController.GetSensitivityMultiplier() : 1f;
        mx *= sensMul;
        my *= sensMul;

        if (mode == ViewMode.TurretLinked)
            yawLocal += mx * sensitivity * dt;
        else
            yawWorld += mx * sensitivity * dt;

        float ySign = invertY ? 1f : -1f;
        pitch += my * sensitivity * ySign * dt;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        ApplyRotation();
    }

    private void HandleViewModeInput()
    {
        if (!enableToggleKey || !Input.GetKeyDown(freeLookToggleKey)) return;

        if (mode != ViewMode.FreeLook)
        {
            yawWorld = yawPivot.eulerAngles.y;
            mode = ViewMode.FreeLook;
        }
        else
        {
            yawLocal = NormalizeAngle(yawPivot.localEulerAngles.y);
            mode = ViewMode.TurretLinked;
        }

        if (debugLog) Debug.Log($"[CommanderView] Mode -> {mode}");
    }

    private void ApplyRotation()
    {
        if (mode == ViewMode.TurretLinked)
            yawPivot.localRotation = Quaternion.Euler(0f, yawLocal, 0f);
        else
            yawPivot.rotation = Quaternion.Euler(0f, yawWorld, 0f);

        pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }

    public bool IsFreeLook => mode == ViewMode.FreeLook;
}