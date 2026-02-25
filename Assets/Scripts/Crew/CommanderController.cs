using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ViewMode
{
    TurretLinked, // 기본: 포탑 종속
    FreeLook      // 자유시점
}


public class CommanderController : MonoBehaviour
{

    [Header("Refs")]
    [SerializeField] private Transform yawPivot;      // CommanderYawPivot
    [SerializeField] private Transform pitchPivot;    // CommanderPitchPivot
    [SerializeField] private Camera commanderCam;     // Main Camera

    [Header("Look")]
    [SerializeField] private float sensitivity = 170f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-20f, 60f);

    [Header("Input")]
    [SerializeField] private bool enableToggleKey = true;
    [SerializeField] private KeyCode freeLookToggleKey = KeyCode.V;     // 고정 토글
    [SerializeField] private bool requireRightMouse = false;            // 우클릭 동안만 회전할지

    [Header("Input")]
    [SerializeField] private KeyCode zoomHoldKey = KeyCode.Mouse1; // 우클릭 홀드
    [SerializeField] private bool wheelOnlyWhileZooming = true;

    [Header("Zoom Levels (WW2 commander binocular style)")]
    [SerializeField] private float[] zoomMagnifications = new float[] { 4f, 6f, 8f };

    [Header("FOV")]
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float zoomFovSmooth = 16f;   // 클수록 빨리 붙음
    [SerializeField] private float minFovClamp = 5f;
    [SerializeField] private float maxFovClamp = 90f;
    [SerializeField] private bool scaleSensitivityByFov = true;
    [SerializeField] private float zoomSensMinMul = 0.12f;   // 너무 느려지지 않게
    [SerializeField] private float zoomSensExponent = 0.85f; // 1=정비례, 0.75~1 추천

    [Header("State")]
    [SerializeField] private bool isZooming;
    [SerializeField] private int zoomIndex = 1; 
    [SerializeField] private ViewMode mode = ViewMode.TurretLinked;

    // 모드별 yaw 저장 (전환 시 튐 방지)
    private float yawLocal;   // 포탑 기준 로컬 yaw
    private float yawWorld;   // 월드 기준 yaw
    private float pitch;

    private float targetFov;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    private void Awake()
    {
        if (!yawPivot) yawPivot = transform;
        if (!pitchPivot) pitchPivot = transform;

        if (!commanderCam) commanderCam = GetComponentInChildren<Camera>();

        if (commanderCam != null)
        {
            baseFov = commanderCam.fieldOfView;
            targetFov = baseFov;
        }

        if (zoomMagnifications == null || zoomMagnifications.Length == 0)
        {
            zoomMagnifications = new float[] { 4f, 6f, 8f };
        }

        zoomIndex = Mathf.Clamp(zoomIndex, 0, zoomMagnifications.Length - 1);

        yawLocal = NormalizeAngle(yawPivot.localEulerAngles.y);
        yawWorld = yawPivot.eulerAngles.y;

        pitch = NormalizeAngle(pitchPivot.localEulerAngles.x);
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
    }

    private void Update()
    {
        HandleViewModeInput();

        if (requireRightMouse && !Input.GetMouseButton(1))
            return;

        float dt = Time.deltaTime;
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");

        float sensMul = GetLookSensitivityMultiplier();
        mx *= sensMul;
        my *= sensMul;

        // Yaw 입력 누적 (모드별로 저장 위치 다름)
        if (mode == ViewMode.TurretLinked)
            yawLocal += mx * sensitivity * dt;
        else
            yawWorld += mx * sensitivity * dt;

        // Pitch 입력
        float ySign = invertY ? 1f : -1f;
        pitch += my * sensitivity * ySign * dt;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        ApplyRotation();
        HandleZoomInput();
        UpdateZoomFov(Time.deltaTime);

    
    }

    private void HandleViewModeInput()
    {
        if (enableToggleKey && Input.GetKeyDown(freeLookToggleKey))
        {
            bool nextFreeLook = (mode != ViewMode.FreeLook);

            if (nextFreeLook)
            {
                // 자유시점 진입: 현재 시선 기준으로 월드 yaw 동기화 (튐 방지)
                yawWorld = yawPivot.eulerAngles.y;
                mode = ViewMode.FreeLook;
            }
            else
            {
                // 포탑연동 복귀: 현재 시선 기준으로 로컬 yaw 동기화 (튐 방지)
                yawLocal = NormalizeAngle(yawPivot.localEulerAngles.y);
                mode = ViewMode.TurretLinked;
            }

            if (debugLog) Debug.Log($"[CommanderView] Mode -> {mode}");
        }
    }

    private void ApplyRotation()
    {
        if (mode == ViewMode.TurretLinked)
        {
            // 포탑 기준 상대 회전
            yawPivot.localRotation = Quaternion.Euler(0f, yawLocal, 0f);
        }
        else
        {
            // 월드 기준 고정 회전 (포탑 회전 영향 무시)
            yawPivot.rotation = Quaternion.Euler(0f, yawWorld, 0f);
        }

        // pitch는 항상 local
        pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public bool IsFreeLook => mode == ViewMode.FreeLook;
    public Camera CommanderCam => commanderCam;

    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }
    private void HandleZoomInput()
    {
        if (Input.GetKeyDown(zoomHoldKey))
        {
            isZooming = !isZooming;
        }

        // 휠로 배율 단계 변경
        bool canWheel = !wheelOnlyWhileZooming || isZooming;
        if (canWheel)
        {
            float wheel = Input.mouseScrollDelta.y;

            if (wheel > 0.01f)
            {
                // 확대 (배율 증가)
                zoomIndex = Mathf.Clamp(zoomIndex + 1, 0, zoomMagnifications.Length - 1);
                if (debugLog) Debug.Log($"[CommanderZoom] Zoom Step Up -> {GetCurrentZoomMag():0.#}x");
            }
            else if (wheel < -0.01f)
            {
                // 축소 (배율 감소)
                zoomIndex = Mathf.Clamp(zoomIndex - 1, 0, zoomMagnifications.Length - 1);
                if (debugLog) Debug.Log($"[CommanderZoom] Zoom Step Down -> {GetCurrentZoomMag():0.#}x");
            }
        }

        // 목표 FOV 계산
        if (isZooming)
        {
            float mag = GetCurrentZoomMag();
            targetFov = Mathf.Clamp(baseFov / Mathf.Max(1f, mag), minFovClamp, maxFovClamp);
        }
        else
        {
            targetFov = Mathf.Clamp(baseFov, minFovClamp, maxFovClamp);
        }
    }

    private void UpdateZoomFov(float dt)
    {
        float a = 1f - Mathf.Exp(-Mathf.Max(0.01f, zoomFovSmooth) * dt);
        commanderCam.fieldOfView = Mathf.Lerp(commanderCam.fieldOfView, targetFov, a);
    }

    private float GetCurrentZoomMag()
    {
        if (zoomMagnifications == null || zoomMagnifications.Length == 0) return 1f;
        int idx = Mathf.Clamp(zoomIndex, 0, zoomMagnifications.Length - 1);
        return Mathf.Max(1f, zoomMagnifications[idx]);
    }
    private float GetLookSensitivityMultiplier()
    {
        if (!scaleSensitivityByFov || commanderCam == null)
            return 1f;

        float ratio = commanderCam.fieldOfView / Mathf.Max(1f, baseFov);
        ratio = Mathf.Clamp01(ratio);

        float scaled = Mathf.Pow(Mathf.Max(0.0001f, ratio), Mathf.Max(0.01f, zoomSensExponent));
        return Mathf.Clamp(scaled, zoomSensMinMul, 1f);
    }
}
