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
    [SerializeField] private KeyCode freeLookHoldKey = KeyCode.LeftAlt; // 누르는 동안 자유시점
    [SerializeField] private bool enableToggleKey = true;
    [SerializeField] private KeyCode freeLookToggleKey = KeyCode.V;     // 고정 토글
    [SerializeField] private bool requireRightMouse = false;            // 우클릭 동안만 회전할지

    [Header("State")]
    [SerializeField] private ViewMode mode = ViewMode.TurretLinked;

    private bool toggleFreeLookLocked = false; // V로 고정된 자유시점 여부

    // 모드별 yaw 저장 (전환 시 튐 방지)
    private float yawLocal;   // 포탑 기준 로컬 yaw
    private float yawWorld;   // 월드 기준 yaw
    private float pitch;

    private void Awake()
    {
        if (!yawPivot) yawPivot = transform;
        if (!pitchPivot) pitchPivot = transform;

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

        // (선택) 정면 복귀
        if (Input.GetKeyDown(KeyCode.R))
        {
            yawLocal = 0f;
            yawWorld = yawPivot.eulerAngles.y;
            pitch = 0f;
            ApplyRotation();
        }
    }

    private void HandleViewModeInput()
    {
        // V 토글 고정
        if (enableToggleKey && Input.GetKeyDown(freeLookToggleKey))
        {
            toggleFreeLookLocked = !toggleFreeLookLocked;
        }

        // Alt 홀드 우선 + 토글 고정 병행
        bool holdFree = Input.GetKey(freeLookHoldKey);
        bool wantFree = holdFree || toggleFreeLookLocked;

        ViewMode targetMode = wantFree ? ViewMode.FreeLook : ViewMode.TurretLinked;
        if (targetMode == mode) return;

        // 모드 전환 순간 현재 시선 기준으로 동기화 (튐 방지)
        if (targetMode == ViewMode.FreeLook)
        {
            yawWorld = yawPivot.eulerAngles.y;
        }
        else // TurretLinked 복귀
        {
            yawLocal = NormalizeAngle(yawPivot.localEulerAngles.y);
        }

        mode = targetMode;
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
}
