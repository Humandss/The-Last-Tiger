using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DriverController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform hull; // 차체

    [Header("Speeds")]
    [SerializeField] private float fwdMaxSpeed = 6.0f;
    [SerializeField] private float bckMaxSpeed = 2.0f;    // m/s (Normal)
    [SerializeField] private float turnSpeedDeg = 45.0f;      // deg/s (Normal) - 이동 중 회전
    [SerializeField] private float pivotSpeedDeg = 90.0f;     // deg/s (Normal) - 제자리 회전

    [Header("Smoothing")]
    [SerializeField] private float accel = 5f;         // m/s^2 가속(3~8)
    [SerializeField] private float steerAccel = 5.0f;
    [SerializeField] private float intensitySharpness = 8f; // 6~12 추천
    [SerializeField] private float speedAccel = 5f;         // m/s^2 가속(3~8)
    [SerializeField] private float brakeAccel = 8f;         // 감속(6~12)
    private float _mulSmoothed = 0.65f; // Normal 시작이면
    private float _curSpeed = 0f; // 실제 현재 속도(m/s)

    [Header("Intensity Keys")]
    [SerializeField] private KeyCode highKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode lowKey = KeyCode.LeftControl;

    // ===== input targets (raw) =====
    private float targetThrottle;   // -1..+1
    private float targetSteer;      // -1..+1
    private float targetPivot;      // -1..+1

    // ===== smoothed state =====
    private float throttle;         // -1..+1
    private float steer;            // -1..+1
    private float pivot;            // -1..+1

    [Header("Intensity Multipliers")]
    [SerializeField] private float smallMul = 0.5f;
    [SerializeField] private float normalMul = 1.0f;
    [SerializeField] private float largeMul = 1.5f;


    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    [SerializeField] private float debugLogInterval = 0.2f;

    private float _debugTimer;
    private Vector3 _prevPos;
    private float _curYawRate; // deg/s (실제)
    private float _prevYaw;

    private void Start()
    {
        _prevPos = hull.position;
        _prevYaw = hull.eulerAngles.y;
    }
    private void Update()
    {
        float dt = Time.deltaTime;
        if (hull == null) hull = transform;

        // 입력은 먼저 처리하는 게 직관적 (이번 프레임 target 갱신 -> 바로 반영)
        DriverHotKeys();

        // 2) 스무딩 (target -> state)
        throttle = SmoothAxis(throttle, targetThrottle, accel, dt);
        steer = SmoothAxis(steer, targetSteer, steerAccel, dt);
        pivot = SmoothAxis(pivot, targetPivot, steerAccel, dt);

        // 3) 강도(mul) 스무딩
        float desiredMul = IntensityMul(CurrentIntensity());
        float a = 1f - Mathf.Exp(-intensitySharpness * dt);
        _mulSmoothed = Mathf.Lerp(_mulSmoothed, desiredMul, a);

        float st = steer * _mulSmoothed;
        float pv = pivot * _mulSmoothed;

        // ===== 1) Pivot 우선: 제자리 회전중이면 속도는 0으로 감속시키고 return
        if (Mathf.Abs(pv) > 0.0001f)
        {
            // pivot 중에도 속도 서서히 0으로 (원하면 brakeAccel로 더 빨리)
            _curSpeed = Mathf.MoveTowards(_curSpeed, 0f, brakeAccel * dt);

            hull.Rotate(0f, pv * pivotSpeedDeg * dt, 0f, Space.World);

            // 디버그용 yawRate는 아래 블록에서 계산하려면 return 전에 prev 갱신이 필요할 수 있음
            if (debugLog) UpdateDebug(dt);
            return;
        }

        // ===== 2) 목표 속도(targetSpeed) 계산
        // throttle의 부호로 전/후진 최대속도 선택
        float max = (throttle >= 0f) ? fwdMaxSpeed : bckMaxSpeed;

        // 강도까지 적용된 "목표 속도"
        float targetSpeed = throttle * max * _mulSmoothed;

        // ===== 3) 실제 속도 상태(_curSpeed)를 가속/감속으로 따라가게
        float accelUse = (Mathf.Abs(targetSpeed) < Mathf.Abs(_curSpeed)) ? brakeAccel : speedAccel;
        _curSpeed = Mathf.MoveTowards(_curSpeed, targetSpeed, accelUse * dt);

        // ===== 4) 이동 적용 (이제 move*max가 아니라 _curSpeed를 사용!)
        if (Mathf.Abs(_curSpeed) > 0.0001f)
            hull.position += hull.forward * (_curSpeed * dt);

        // ===== 5) 주행 중 회전
        if (Mathf.Abs(st) > 0.0001f)
        {
            float turnMul = (_curSpeed < 0f) ? 0.6f : 1.0f; // 후진시 조향 약화
            hull.Rotate(0f, st * turnSpeedDeg * turnMul * dt, 0f, Space.World);
        }

        // ===== Debug
        if (debugLog) UpdateDebug(dt);
    }

    public void SetThrottle(float throttle01) => throttle = Mathf.Clamp(throttle01, -1f, 1f);
    public void SetSteer(float steer01) => steer = Mathf.Clamp(steer01, -1f, 1f);
    public void SetPivot(float pivot01) => pivot = Mathf.Clamp(pivot01, -1f, 1f);

    public void ClearSteer() => steer = 0f;
    public void ClearPivot() => pivot = 0f;
    public void Stop()
    {
        throttle = 0f;
        steer = 0f;
        pivot = 0f;
    }

    public void DriverHotKeys()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Stop();
            targetThrottle = 0f;
            targetSteer = 0f;
            targetPivot = 0f;
            return;
        }

        // throttle: W/S
        float t = 0f;
        if (Input.GetKey(KeyCode.W)) t += 1f;
        if (Input.GetKey(KeyCode.S)) t -= 1f;

        // pivot: Q/E (제자리 회전)
        float p = 0f;
        if (Input.GetKey(KeyCode.Q)) p -= 1f;
        if (Input.GetKey(KeyCode.E)) p += 1f;

        // steer: A/D (주행 회전)
        float s = 0f;
        if (Input.GetKey(KeyCode.A)) s -= 1f;
        if (Input.GetKey(KeyCode.D)) s += 1f;

        // pivot이 있으면 steer는 의미 없게(원하면 유지해도 됨)
        if (Mathf.Abs(p) > 0.0001f) s = 0f;

        targetThrottle = Mathf.Clamp(t, -1f, 1f);
        targetSteer = Mathf.Clamp(s, -1f, 1f);
        targetPivot = Mathf.Clamp(p, -1f, 1f);
    }

    private Intensity CurrentIntensity()
    {
        bool hi = Input.GetKey(highKey);
        bool lo = Input.GetKey(lowKey);

        if (hi && !lo) return Intensity.Large;
        if (lo && !hi) return Intensity.Small;
        return Intensity.Normal;
    }
    private float IntensityMul(Intensity i)
    {
        switch (i)
        {
            case Intensity.Small: return smallMul;
            case Intensity.Large: return largeMul;
            default: return normalMul;
        }
    }
    private static float SmoothAxis(float cur, float target, float sharpness, float dt)
    {
        sharpness = Mathf.Max(0.0001f, sharpness);
        // Exponential smoothing: 프레임레이트 독립
        float a = 1f - Mathf.Exp(-sharpness * dt);
        return Mathf.Lerp(cur, target, a);
    }
    // 디버그 계산은 함수로 빼두는게 깔끔
    private void UpdateDebug(float dt)
    {
        // yawRate(deg/s)
        float yaw = hull.eulerAngles.y;
        float dyaw = Mathf.DeltaAngle(_prevYaw, yaw);
        _curYawRate = (dt > 1e-6f) ? (dyaw / dt) : 0f;
        _prevYaw = yaw;

        _debugTimer -= dt;
        if (_debugTimer <= 0f)
        {
            _debugTimer = debugLogInterval;

            Debug.Log(
                $"[DriverDBG] thr={throttle:0.00} mul={_mulSmoothed:0.00} " +
                $"curSpd={_curSpeed:0.00}m/s yawRate={_curYawRate:0.0}deg/s pos={hull.position:F1}"
            );
        }
    }
}
