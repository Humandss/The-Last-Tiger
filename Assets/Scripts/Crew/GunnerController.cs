using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UI;


public interface ITankGunner
{
    void Aim();                
    void AlignHull();        
    void SetRange(float meters);
    void CeaseAction();
    void Fire();
}

public class GunnerController : MonoBehaviour, ITankGunner
{
    [Header("Refs")]

    [SerializeField] private Camera commanderCam;
    [SerializeField] private Transform hull;
    [SerializeField] private Transform turretYaw;
    [SerializeField] private Transform gunPitch;

    [Header("Aim")]
    private LayerMask aimMask = ~0;
    private float maxAimDistance = 8000f;
    private float rangeMeters = 800f;

    [SerializeField] private float yawSpeedDeg = 120f;
    [SerializeField] private float pitchSpeedDeg = 90f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-10f, 20f);

    public Vector3? LastPoint { get; private set; }
    [Header("Optional Marker")]
    public Transform marker;
    Coroutine _aimCo;


    private bool _aiming;
    private Vector3 _aimPoint;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var ray = commanderCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
            {
                LastPoint = hit.point;
                Debug.Log($"[Designator] point = {hit.point}");

            }
            else
            {
                // 아무것도 안 맞으면 저장 안 하거나, 전방 maxDistance로 저장(선택)
                Debug.Log("[Designator] no hit");
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) Aim();
        

        if (Input.GetKeyDown(KeyCode.Alpha2)) AlignHull();
  
        if (Input.GetKeyDown(KeyCode.Alpha3)) CeaseAction();
     
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            rangeMeters = Mathf.Max(5f, rangeMeters - 50f);
            Debug.Log($"[GunnerDebug] Range = {rangeMeters:0}m");
        }
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            rangeMeters = Mathf.Min(2000f, rangeMeters + 50f);
            Debug.Log($"[GunnerDebug] Range = {rangeMeters:0}m");
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log($"[GunnerDebug] Range = {rangeMeters:0}m");
        }

        // ===== 실행(조준 추적) =====
       if (_aiming) AimAtWorldPoint(_aimPoint);
   
    }

    public void Aim()
    {
        if (LastPoint.HasValue)
        {
            _aiming = true;
            _aimPoint = LastPoint.Value;
        }
        else
        {
            Debug.LogWarning("[Gunner] 저장된 지점이 없어. 먼저 클릭으로 지점 지정해줘.");
            return;
        }
        Debug.Log($"[Gunner] Aim -> {LastPoint}");
    }

    public void AlignHull()
    {
        StopAimRoutine();
        _aiming = false;
        // yaw를 차체 정면으로, pitch는 기본값(0)로
        var yaw = turretYaw.eulerAngles;
        yaw.y = hull.eulerAngles.y;
        turretYaw.eulerAngles = yaw;

        var pitch = gunPitch.localEulerAngles;
        pitch.x = 0f;
        gunPitch.localEulerAngles = pitch;

        Debug.Log("[Gunner] Align Hull");
    }

    public void CeaseAction()
    {
        StopAimRoutine();
        _aiming = false;
        Debug.Log("[Gunner] Cease Action");
    }

    public void SetRange(float meters)
    {
        rangeMeters = Mathf.Clamp(meters, 5f, 2000f);
        Debug.Log($"[Gunner] Range = {rangeMeters:0}m");
    }

    private void StopAimRoutine()
    {
        if (_aimCo != null) { StopCoroutine(_aimCo); _aimCo = null; }
    }

    private void AimAtWorldPoint(Vector3 worldPoint)
    {
        // Yaw
        Vector3 to = worldPoint - turretYaw.position;
        Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
        if (flat.sqrMagnitude > 0.0001f)
        {
            Quaternion targetYaw = Quaternion.LookRotation(flat, Vector3.up);
            turretYaw.rotation = Quaternion.RotateTowards(turretYaw.rotation, targetYaw, yawSpeedDeg * Time.deltaTime);
        }

        // Pitch (turretYaw 기준 local)
        Vector3 localDir = turretYaw.InverseTransformDirection(to.normalized);
        float pitch = -Mathf.Atan2(localDir.y, new Vector2(localDir.x, localDir.z).magnitude) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        float cur = NormalizeAngle(gunPitch.localEulerAngles.x);
        float next = Mathf.MoveTowardsAngle(cur, pitch, pitchSpeedDeg * Time.deltaTime);

        var e = gunPitch.localEulerAngles;
        e.x = next;
        gunPitch.localEulerAngles = e;
    }
    public void Fire()
    {
        Debug.Log("[Gunner] Fire");
        // TODO: weapon.Fire();
    }
    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }
}
