using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DriverController : MonoBehaviour
{
    [SerializeField] private bool isAI = false;

    [Header("Refs")]
    [SerializeField] private Transform hull; 
    [SerializeField] private Rigidbody rb;

    [Header("Speeds")]
    [SerializeField] private float fwdMaxSpeed = 6.0f;   // m/s 
    [SerializeField] private float bckMaxSpeed = 2.0f;   // m/s
    [SerializeField] private float turnSpeedDeg = 45.0f; // deg/s 
    [SerializeField] private float pivotSpeedDeg = 90.0f;

    [Header("Smoothing")]
    [SerializeField] private float axisSharpness = 10f;     
    [SerializeField] private float speedAccel = 5f;          
    [SerializeField] private float brakeAccel = 8f;          
    [SerializeField] private float intensitySharpness = 8f;  

    [Header("Mobility (from tracks)")]
    [SerializeField] private bool leftTrackDestroyed;
    [SerializeField] private bool rightTrackDestroyed;
    private bool ImmobilizedByTracks => (leftTrackDestroyed && rightTrackDestroyed); 

    [Header("Mobility (from engine or transmission)")]
    [SerializeField, Range(0f, 1f)] private float mobilityMul = 1f; 
    [SerializeField] private bool immobilized = false;              
    [SerializeField] private float mobilitySmooth = 8f;            
    private float mobilityMulSmoothed = 1f;

    [Header("Intensity Keys")]
    [SerializeField] private KeyCode highKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode lowKey = KeyCode.LeftControl;

    [Header("Intensity Multipliers")]
    [SerializeField] private float smallMul = 0.35f;
    [SerializeField] private float normalMul = 0.65f;
    [SerializeField] private float largeMul = 1.00f;

    [SerializeField, Range(0f, 1f)] private float driverMul = 1f;   
    [SerializeField] private bool driverDead = false;
    [SerializeField] private bool driverTaskBlocked = false;
    private float driverMulSmoothed = 1f;


    // ===== raw targets  =====
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

    public float CurrentSpeed => _curSpeed;
    public float CurrentThrottle => throttle;
    public float TargetThrottle => targetThrottle;
    public float TargetPivot => targetPivot;
    public float TargetSteer => targetSteer;

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

    
        float ma = 1f - Mathf.Exp(-mobilitySmooth * dt);
        mobilityMulSmoothed = Mathf.Lerp(mobilityMulSmoothed, mobilityMul, ma);

        float da = 1f - Mathf.Exp(-mobilitySmooth * dt);
        driverMulSmoothed = Mathf.Lerp(driverMulSmoothed, driverMul, da);


        if(!isAI) DriverHotKeys();

        if (driverDead || driverTaskBlocked)
        {
            StopAll();
            Debug.Log("[Driver] 조종 불능!");
            return;
        }


        if (immobilized)
        {
            StopAll();
            Debug.Log("[Driver] 기동 불가!");
            return;
        }

        if (leftTrackDestroyed || rightTrackDestroyed)
        {

            if (ImmobilizedByTracks)
            {
                StopAll();
                Debug.Log("[Driver] 궤도 수리 요망!");
                return;
            }

            targetThrottle = 0f;

        }

        throttle = Smooth(throttle, targetThrottle, axisSharpness, dt);
        steer = Smooth(steer, targetSteer, axisSharpness, dt);
        pivot = Smooth(pivot, targetPivot, axisSharpness, dt);

        float desiredMul = IntensityMul(CurrentIntensity());
        float ia = 1f - Mathf.Exp(-intensitySharpness * dt);
        _mulSmoothed = Mathf.Lerp(_mulSmoothed, desiredMul, ia);

        float pv = pivot * _mulSmoothed;
        if (Mathf.Abs(pv) > 0.0001f)
        {
            Quaternion nextRot = rb.rotation * Quaternion.Euler(0f, pv * pivotSpeedDeg * driverMulSmoothed * dt, 0f);
            rb.MoveRotation(nextRot);
            return;
        }

 
        float max = (throttle >= 0f) ? fwdMaxSpeed : bckMaxSpeed;
        float targetSpeed = throttle * max * _mulSmoothed * mobilityMulSmoothed * driverMulSmoothed;


        float accelUse = (Mathf.Abs(targetSpeed) < Mathf.Abs(_curSpeed)) ? brakeAccel : speedAccel;
        _curSpeed = Mathf.MoveTowards(_curSpeed, targetSpeed, accelUse * dt);

        if (Mathf.Abs(_curSpeed) > 0.0001f)
        {
            Vector3 nextPos = rb.position + (rb.transform.forward * (_curSpeed * dt));
            rb.MovePosition(nextPos);
        }

        float st = steer * _mulSmoothed;
        if (Mathf.Abs(st) > 0.0001f)
        {
            float turnMul = (_curSpeed < 0f) ? 0.6f : 1.0f;
            Quaternion nextRot = rb.rotation * Quaternion.Euler(0f, st * turnSpeedDeg * turnMul * dt, 0f);
            rb.MoveRotation(nextRot);
        }

        UpdateDebugFixed(dt, hull.position, hull.rotation);
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

    public void DriverHotKeys()
    {
        if (PlayerCrewInteriorOverlay.IsInputCaptured)
        {
            StopAll();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StopAll();
            return;
        }

        float t = 0f;
        if (Input.GetKey(KeyCode.W)) t += 1f;
        if (Input.GetKey(KeyCode.S)) t -= 1f;


        float s = 0f;
        if (Input.GetKey(KeyCode.A)) s -= 1f;
        if (Input.GetKey(KeyCode.D)) s += 1f;

        targetThrottle = Mathf.Clamp(t, -1f, 1f);
        targetSteer = Mathf.Clamp(s, -1f, 1f);
  
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

    public void SetDriverTaskBlocked(bool blocked)
    {
        driverTaskBlocked = blocked;
        if (blocked)
            StopAll();
    }

    public void SetInput(float throttle, float steer, float pivot)
    {
        targetThrottle = Mathf.Clamp(throttle, -1f, 1f);
        targetSteer = Mathf.Clamp(steer, -1f, 1f);
        targetPivot = Mathf.Clamp(pivot, -1f, 1f);
    }
}


