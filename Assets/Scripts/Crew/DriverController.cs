using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DriverController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform hull; // 차체(없으면 transform)
    [SerializeField] private Rigidbody rb;

    [Header("Speeds")]
    [SerializeField] private float fwdMaxSpeed = 6.0f;   // m/s (Large=1.0일 때 기준)
    [SerializeField] private float bckMaxSpeed = 2.0f;   // m/s
    [SerializeField] private float turnSpeedDeg = 45.0f; // deg/s (주행 회전)
    [SerializeField] private float pivotSpeedDeg = 90.0f;// deg/s (제자리 회전)

    [Header("Smoothing")]
    [SerializeField] private float axisSharpness = 10f;      // W/A/S/D/Q/E 입력 스무딩
    [SerializeField] private float speedAccel = 5f;          // 목표속도 따라가기 가속
    [SerializeField] private float brakeAccel = 8f;          // 감속
    [SerializeField] private float intensitySharpness = 8f;  // 강도 스무딩

    [Header("Mobility (from tracks)")]
    [SerializeField] private bool leftTrackDestroyed;
    [SerializeField] private bool rightTrackDestroyed;
    private bool ImmobilizedByTracks => (leftTrackDestroyed && rightTrackDestroyed); // 둘다 파괴

    [Header("Mobility (from engine or transmission)")]
    [SerializeField, Range(0f, 1f)] private float mobilityMul = 1f; // 엔진/미션 체력 기반
    [SerializeField] private bool immobilized = false;              // 둘중 하나 파괴면 true
    [SerializeField] private float mobilitySmooth = 8f;             // 배율 변화 부드럽게
    private float mobilityMulSmoothed = 1f;

    [Header("Intensity Keys")]
    [SerializeField] private KeyCode highKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode lowKey = KeyCode.LeftControl;

    [Header("Intensity Multipliers")]
    [SerializeField] private float smallMul = 0.35f;
    [SerializeField] private float normalMul = 0.65f;
    [SerializeField] private float largeMul = 1.00f;

    [SerializeField, Range(0f, 1f)] private float driverMul = 1f;   // 운전수 체력 비율
    [SerializeField] private bool driverDead = false;
    private float driverMulSmoothed = 1f;


    // ===== raw targets (명령/키 입력이 세팅) =====
    float targetThrottle; // -1..+1
    float targetSteer;    // -1..+1
    float targetPivot;    // -1..+1

    // ===== smoothed axes =====
    float throttle;       // -1..+1
    float steer;          // -1..+1
    float pivot;          // -1..+1

    // ===== intensity smoothed =====
    float _mulSmoothed = 0.65f;

    // ===== speed state =====
    float _curSpeed = 0f; // m/s


    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    [SerializeField] private float debugLogInterval = 0.25f;

    private float _dbgTimer;
    private Vector3 _prevPos;
    private float _prevYaw;

    private float _lastSpeed;   // measured m/s
    private float _lastYawRate; // measured deg/s


    private void Awake()
    {
        if (!hull) hull = transform;
    }

    private void FixedUpdate()
    {
        float dt = Time.deltaTime;

        // 모빌리티 배율 스무딩
        float ma = 1f - Mathf.Exp(-mobilitySmooth * dt);
        mobilityMulSmoothed = Mathf.Lerp(mobilityMulSmoothed, mobilityMul, ma);
        //운전수 체력 배율 스무딩
        float da = 1f - Mathf.Exp(-mobilitySmooth * dt);
        driverMulSmoothed = Mathf.Lerp(driverMulSmoothed, driverMul, da);

        // (디버깅용) 키 입력 -> target 갱신
        DriverHotKeys();

        if (driverDead)
        {
            StopAll();
            Debug.Log("[Driver] 운전수 사망 -> 기동불가!");
            return;
        }

        // 기동 불가면 입력/속도 다 끊고 멈추기
        if (immobilized)
        {
            StopAll();
            Debug.Log("[Driver] 기동 모듈 파괴 기동불가!");
            return;
        }

        if (leftTrackDestroyed || rightTrackDestroyed)
        {
            // 둘 다 파괴면 완전 정지
            if (ImmobilizedByTracks)
            {
                StopAll();
                Debug.Log("[Driver] 궤도 파괴 기동불가!");
                return;
            }

            // 한쪽만 파괴: 피벗 외 이동은 막음
            targetThrottle = 0f;

        }


        // 입력축 스무딩
        throttle = Smooth(throttle, targetThrottle, axisSharpness, dt);
        steer = Smooth(steer, targetSteer, axisSharpness, dt);
        pivot = Smooth(pivot, targetPivot, axisSharpness, dt);

        // 강도 스무딩
        float desiredMul = IntensityMul(CurrentIntensity());
        float ia = 1f - Mathf.Exp(-intensitySharpness * dt);
        _mulSmoothed = Mathf.Lerp(_mulSmoothed, desiredMul, ia);

        // 제자리 회전 우선: pivot 있으면 속도 0으로 감속 + 회전만
        float pv = pivot * _mulSmoothed;
        if (Mathf.Abs(pv) > 0.0001f)
        {
            Quaternion nextRot = rb.rotation * Quaternion.Euler(0f, pv * pivotSpeedDeg * driverMulSmoothed * dt, 0f);
            rb.MoveRotation(nextRot);
            return;
        }

        // 목표 속도 계산(강도 포함)
        float max = (throttle >= 0f) ? fwdMaxSpeed : bckMaxSpeed;
        float targetSpeed = throttle * max * _mulSmoothed * mobilityMulSmoothed * driverMulSmoothed;

        // 실제 속도 상태(_curSpeed)를 가감속으로 따라가게
        float accelUse = (Mathf.Abs(targetSpeed) < Mathf.Abs(_curSpeed)) ? brakeAccel : speedAccel;
        _curSpeed = Mathf.MoveTowards(_curSpeed, targetSpeed, accelUse * dt);

        // 이동
        if (Mathf.Abs(_curSpeed) > 0.0001f)
        {
            Vector3 nextPos = rb.position + (rb.transform.forward * (_curSpeed * dt));
            rb.MovePosition(nextPos);
        }

        // 주행 회전
        float st = steer * _mulSmoothed;
        if (Mathf.Abs(st) > 0.0001f)
        {
            float turnMul = (_curSpeed < 0f) ? 0.6f : 1.0f;
            Quaternion nextRot = rb.rotation * Quaternion.Euler(0f, st * turnSpeedDeg * turnMul * dt, 0f);
            rb.MoveRotation(nextRot);
        }

        UpdateDebugFixed(dt, hull.position, hull.rotation);
    }

    // ====== 외부(디스패처/보이스)에서 쓰는 API ======
    public void SetDesired(float thr, float st, float pv)
    {
        targetThrottle = Mathf.Clamp(thr, -1f, 1f);
        targetSteer = Mathf.Clamp(st, -1f, 1f);
        targetPivot = Mathf.Clamp(pv, -1f, 1f);
    }

    public void StopAll()
    {
        targetThrottle = 0f;
        targetSteer = 0f;
        targetPivot = 0f;

        throttle = 0f;
        steer = 0f;
        pivot = 0f;

        _curSpeed = 0f;
    }

    // ====== 디버깅용 키입력 ======
    public void DriverHotKeys()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StopAll();
            return;
        }

        float t = 0f;
        if (Input.GetKey(KeyCode.W)) t += 1f;
        if (Input.GetKey(KeyCode.S)) t -= 1f;

        //float p = 0f;
        //if (Input.GetKey(KeyCode.Q)) p -= 1f;
        // if (Input.GetKey(KeyCode.E)) p += 1f;

        float s = 0f;
        if (Input.GetKey(KeyCode.A)) s -= 1f;
        if (Input.GetKey(KeyCode.D)) s += 1f;

        // if (Mathf.Abs(p) > 0.001f) s = 0f;

        targetThrottle = Mathf.Clamp(t, -1f, 1f);
        targetSteer = Mathf.Clamp(s, -1f, 1f);
        //targetPivot = Mathf.Clamp(p, -1f, 1f);
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
        return i switch
        {
            Intensity.Small => smallMul,
            Intensity.Large => largeMul,
            _ => normalMul
        };
    }

    private static float Smooth(float cur, float target, float sharpness, float dt)
    {
        float a = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * dt);
        return Mathf.Lerp(cur, target, a);
    }

    private void UpdateDebugFixed(float dt, Vector3 curPos, Quaternion curRot)
    {
        if (!debugLog) return;

        // measured speed (m/s)
        Vector3 dp = curPos - _prevPos;
        _lastSpeed = (dt > 1e-6f) ? (dp.magnitude / dt) : 0f;
        _prevPos = curPos;

        // measured yawRate (deg/s)
        float yaw = curRot.eulerAngles.y;
        float dyaw = Mathf.DeltaAngle(_prevYaw, yaw);
        _lastYawRate = (dt > 1e-6f) ? (dyaw / dt) : 0f;
        _prevYaw = yaw;

        _dbgTimer -= dt;
        if (_dbgTimer <= 0f)
        {
            _dbgTimer = debugLogInterval;

            /* Debug.Log(
                 $"[DriverDBG] thr={throttle:0.00} steer={steer:0.00} pivot={pivot:0.00} " +
                 $"mul={_mulSmoothed:0.00} targetSpd={(_curSpeed):0.00}m/s measSpd={_lastSpeed:0.00}m/s " +
                 $"yawRate={_lastYawRate:0.0}deg/s pos={curPos:F1}"
             );*/
        }
    }


    public void SetMobilityModuleState(bool canMove, float maxSpeedMul01)
    {
        immobilized = !canMove;
        mobilityMul = Mathf.Clamp01(maxSpeedMul01);
    }
    public void SetTrackState(bool left, bool right)
    {
        leftTrackDestroyed = left;
        rightTrackDestroyed = right;
    }
    public void SetDriverState(bool dead, float hpRatio)
    {
        driverDead = dead;
        driverMul = Mathf.Lerp(0.6f, 1.0f, hpRatio);
    }
}
