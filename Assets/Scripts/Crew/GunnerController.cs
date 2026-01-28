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
    private LoaderController loader;
    private CannonFireController fireController;
    private ITankLoader loaderFunc;

    [Header("Aim")]
    private Vector3? targetPoint;
    private LayerMask aimMask = ~0;
    private float maxAimDistance = 5000f;
    private float rangeMeters = 800f;
    private bool isAiming;
    private Vector3 aimPoint;
    private bool isAligning;


    [SerializeField] private float yawSpeedDeg = 120f;
    [SerializeField] private float pitchSpeedDeg = 90f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-10f, 20f);


    private void Awake()
    {
        loader = GetComponent<LoaderController>();
        fireController = GetComponent<CannonFireController>();


        loaderFunc = loader as ITankLoader;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var ray = commanderCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
                Debug.Log($"[Designator] point = {hit.point}");

            }
            else
            {
                // 아무것도 안 맞으면 저장 안 하거나, 전방 maxDistance로 저장(선택)
                Debug.Log("[Designator] no hit");
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) Aim();
        

        if (Input.GetKeyDown(KeyCode.Alpha2)) Fire();
  


        // ===== 실행(조준 추적) =====
        if (isAligning)
        {
            if (AlignHullStep())isAligning = false;
      
        }
        else if (isAiming) AimAtWorldPoint(aimPoint);

    }

    public void Aim()
    {
        if (targetPoint.HasValue)
        {
            isAiming = true;
            aimPoint = targetPoint.Value;
        }
        else
        {
            Debug.LogWarning("[Gunner] 저장된 지점이 없어. 먼저 클릭으로 지점 지정해줘.");
            return;
        }
        Debug.Log($"[Gunner] 에임 포인트 -> {targetPoint}");
    }

    public void AlignHull()
    {
        isAiming = false;
        isAligning = true;

        Debug.Log("[Gunner] 포신 정렬!");
    }
    private bool AlignHullStep()
    {
        //차체 yaw
        float targetYaw = hull.eulerAngles.y;

        // 현재 turret yaw를 목표로 조금씩 이동
        var y = turretYaw.eulerAngles;
        y.y = Mathf.MoveTowardsAngle(y.y, targetYaw, yawSpeedDeg * Time.deltaTime);
        turretYaw.eulerAngles = y;

        // pitch는 0도로 조금씩 이동 (local)
        float curPitch = NormalizeAngle(gunPitch.localEulerAngles.x);
        float nextPitch = Mathf.MoveTowardsAngle(curPitch, 0f, pitchSpeedDeg * Time.deltaTime);
        var p = gunPitch.localEulerAngles;
        p.x = nextPitch;
        gunPitch.localEulerAngles = p;

        // 완료 판정(각도 차이 거의 없으면 종료)
        bool yawDone = Mathf.Abs(Mathf.DeltaAngle(y.y, targetYaw)) < 0.5f;
        bool pitchDone = Mathf.Abs(Mathf.DeltaAngle(nextPitch, 0f)) < 0.5f;

        return yawDone && pitchDone;
    }
    public void CeaseAction()
    {
        isAiming = false;
        isAligning = false;
        Debug.Log("[Gunner] 행동 취소!");
    }

    public void SetRange(float meters)
    {
        if(meters > maxAimDistance)
        {
            Debug.Log($"[Gunner] 사거리 한도 초과! 최대 사거리{maxAimDistance}에 맞춥니다.");
            meters = maxAimDistance;
        }
        rangeMeters = Mathf.Clamp(meters, 5f, maxAimDistance);
        Debug.Log($"[Gunner] 사거리 = {rangeMeters:0}m");
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
        if (!loaderFunc.GetIsLoaded() || loaderFunc.GetIsLoading())
        {
            Debug.Log("[Gunner] 장전이 되지 않았습니다! 사격 불가");
            return;
        }

        Debug.Log("[Gunner] 발사!");
        fireController.FireProjectile();

        loaderFunc.IsShot();
        loaderFunc.LoadDefault();
    }
    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }
}
