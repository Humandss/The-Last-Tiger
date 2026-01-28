using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

public class BallisticManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ShellData shell;
    private LayerMask layerMask;

    [Header("Bullet Value")]
    private int id = 0; // 총알 아이디
    private static int idSeq = 0;
    private Vector3 velocity; //벡터 속력
    private Vector3 pos; //현 위치
    private Vector3 prevPos; // 이전 위치
    private Vector3 dir; //총알 방향
    private float refArea; //총알 면적
    private float flightTime;
    private int ricochetChance = 0;
    private float speed; //총알 속도
    private float pen; //총알 관통력
    private bool isPenetratingTerrain = false;

    [Header("World")]
    private float airDensity = 1.225f;
    private Vector3 windWorld = Vector3.zero;
    private float k; // 공기저항


#if true// 탄 트레일 남기는 로직
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private Light tracerLight;
    [SerializeField] private float igniteDelay = 0.08f;
    [SerializeField] private float burnTime = 2.0f;

    void Awake()
    {
        if (trail) trail.emitting = false;
        if (tracerLight) tracerLight.enabled = false;
    }

    void OnEnable()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(igniteDelay);

        if (trail) trail.emitting = true;
        if (tracerLight) tracerLight.enabled = true;

        yield return new WaitForSeconds(burnTime);

        if (trail) trail.emitting = false;
        if (tracerLight) tracerLight.enabled = false;
    }
#endif

    public void Initialize(Vector3 position, Vector3 direction)
    {
        id = System.Threading.Interlocked.Increment(ref idSeq);
        isPenetratingTerrain = false;
        ricochetChance = 0;
        flightTime = 0.0f;
        pos = position;
        prevPos = pos;
        dir = direction.normalized;
        pen = shell.penetrationPower;

        velocity = dir * shell.muzzleVelocity;   // 초기 속도 

        float invMass = 1.0f / Mathf.Max(1e-6f, shell.projectileMass); // 1/중량

        float r = Mathf.Max(1e-6f, (shell.caliber * 0.001f)) * 0.5f; // m로 바꾸기
        refArea = Mathf.PI * r * r * shell.refAreaScale; // 단면적(m)

        k = 0.5f * airDensity * shell.dragCoeff * refArea * invMass;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        flightTime += dt;
        if (flightTime > shell.lifeTime) { Destroy(gameObject); return; }

        prevPos = pos;
        //바람 저항
        Vector3 vRel = velocity - windWorld;
        //숫자 0이 되지 않게끔
        speed = vRel.magnitude + 1e-6f;
        //중력 계수*공기저항
        Vector3 g = Physics.gravity + (-k * vRel * speed);

        //속도 및 포지션 변환
        velocity += g * dt;
        pos += velocity * dt;

        //HandleImpact(prevPos);

        transform.position = pos;

        //Debug.Log($"ammo type ={ammo.name}, pos={pos}, Vector_velocity={velocity}, time={flightTime}, distance={(flightTime*velocity).z}");
    }
}
