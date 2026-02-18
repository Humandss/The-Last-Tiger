using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretBlowOff : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform turretRoot;
    [SerializeField] private LayerMask groundMask;

    [Header("Launch")]
    [SerializeField] private float upSpeed = 14f;
    [SerializeField] private float outSpeed = 10f;
    [SerializeField] private float outSpeedRandom = 2.5f;
    [SerializeField] private float gravity = 18f;        
    [SerializeField] private float maxFlightTime = 6f;

    [Header("Spin")]
    [SerializeField] private Vector3 spinDegPerSec = new Vector3(80f, 260f, 40f);
    [SerializeField] private float spinDamp = 0.92f;       //  착지 후 회전 감쇠

    [Header("Ground / Impact")]
    [SerializeField] private float groundRayHeight = 2.0f;
    [SerializeField] private float groundRayLen = 8.0f;

    [SerializeField, Range(0f, 0.6f)] private float bounce = 0.18f; 
    [SerializeField] private float impactLoss = 0.55f;     

    [Header("Slide")]
    [SerializeField] private float groundFriction = 6.0f;  
    [SerializeField] private float stopSpeed = 0.25f;      

    private bool flying;
    private bool grounded;
    private Vector3 vel;
    private Vector3 spin;
    private float life;

    public void BlowOff(Vector3 explosionWorldPos)
    {
        
        if (flying) return;
        if (!turretRoot) return;

        turretRoot.SetParent(null, true);

        foreach (var mb in turretRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {

            if (mb == this) continue;
            mb.enabled = false;
        }

        Vector3 outDir = (turretRoot.position - explosionWorldPos);
        outDir.y = 0f;
        outDir = (outDir.sqrMagnitude > 0.0001f) ? outDir.normalized : turretRoot.forward;

        float outRand = Random.Range(-outSpeedRandom, outSpeedRandom);
        vel = Vector3.up * upSpeed + outDir * (outSpeed + outRand);

        //  시작 박힘 방지: 더 과감히 밖으로/위로 빼기
        turretRoot.position += Vector3.up * 0.9f + outDir * 1.8f;

        //  스핀도 약간 랜덤
        spin = new Vector3(
        Random.Range(80f, 200f),
        Random.Range(250f, 700f),
        Random.Range(40f, 160f)
         );

        flying = true;
        grounded = false;
        life = 0f;

      

    }
    
    private void Update()
    {
        if (!flying || !turretRoot) return;

        Debug.Log($"flying={flying} grounded={grounded} spinMag={spin.magnitude:0.0} vel={vel}");

        float dt = Time.deltaTime;
        life += dt;

        // 회전(공중/지상 둘 다 적용하되, 지상에서 감쇠됨)
        turretRoot.Rotate(spin * dt, Space.Self);
        if (grounded) spin *= Mathf.Pow(spinDamp, dt * 60f); // 프레임레이트 독립 감쇠

        // 중력(공중일 때만 강하게, 지상에서는 y속도 0 유지)
        if (!grounded)
            vel += Vector3.down * gravity * dt;

        Vector3 nextPos = turretRoot.position + vel * dt;
        float groundEps = 0.05f;
        // 바닥 체크
        if (TryGetGroundY(nextPos, out float groundY))
        {
            if (nextPos.y <= groundY + groundEps)
            {
                nextPos.y = groundY;

                if (!grounded)
                {
                    //  첫 착지(임팩트)
                    grounded = true;

                    // 수직 속도는 반사(바운스), 수평은 손실
                    float vy = vel.y;
                    vel.y = Mathf.Abs(vy) * bounce;

                    Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
                    horizontal *= (1f - impactLoss);
                    vel.x = horizontal.x;
                    vel.z = horizontal.z;

                    // 착지 순간 "쿵" 느낌: y는 거의 안 튀게 하고 싶으면 bounce를 0.05~0.12로
                    // 좀 더 박력은 impactLoss를 0.6~0.75로
                }
                else
                {
                    // 지상 슬라이드(마찰)
                    Vector3 h = new Vector3(vel.x, 0f, vel.z);
                    float spd = h.magnitude;

                    if (spd > 1e-4f)
                    {
                        float decel = groundFriction * dt;
                        float newSpd = Mathf.Max(0f, spd - decel);
                        h = (newSpd > 0f) ? h.normalized * newSpd : Vector3.zero;
                        vel.x = h.x;
                        vel.z = h.z;
                    }

                    // y는 바닥에 붙게
                    vel.y = 0f;

                    //거의 멈췄으면 종료
                    if (new Vector3(vel.x, 0f, vel.z).magnitude <= stopSpeed && vel.y <= 0.01f)
                    {
                        flying = false;
                        vel = Vector3.zero;
                        return;
                    }
                }
            }
            else
            {
                // 공중
                grounded = false;
            }
        }

        turretRoot.position = nextPos;

        if (life >= maxFlightTime)
        {
            flying = false;
            vel = Vector3.zero;
        }
    }

    private bool TryGetGroundY(Vector3 at, out float y)
    {
        Vector3 rayStart = new Vector3(at.x, at.y + groundRayHeight, at.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRayLen, groundMask, QueryTriggerInteraction.Ignore))
        {
            y = hit.point.y;
            return true;
        }
        y = at.y;
        return false;
    }
}
