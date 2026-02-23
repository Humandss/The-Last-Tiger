using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShotRecoilShake : MonoBehaviour
{
    [Header("Rotation Kick (deg)")]
    [SerializeField] private Vector3 maxKickEuler = new Vector3(1.2f, 0.8f, 0.25f);
    [SerializeField] private bool randomizeYawRoll = true;

    [Header("Motion")]
    [SerializeField] private float kickInSpeed = 28f;     // 맞는 순간
    [SerializeField] private float returnSpeed = 10f;     // 복귀
    [SerializeField] private float damping = 16f;         // 잔진동 감쇠
    [SerializeField] private float noiseFreq = 22f;       // 잔진동 주파수
    [SerializeField] private float noiseAmount = 0.20f;   // 잔진동 세기(0~1)

    [Header("Axis Mask")]
    [SerializeField] private bool affectPitch = true;
    [SerializeField] private bool affectYaw = true;
    [SerializeField] private bool affectRoll = true;

    private Quaternion _baseLocalRot;

    private Vector3 _curEuler;       // 현재 흔들림 오프셋(deg)
    private Vector3 _targetEuler;    // 목표 킥(deg)
    private float _noiseSeed;
    private bool _returning;

    private void Awake()
    {
        _baseLocalRot = transform.localRotation;
        _noiseSeed = Random.value * 1000f;
    }

    private void OnEnable()
    {
        _baseLocalRot = transform.localRotation;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 킥 인 / 복귀
        float speed = _returning ? returnSpeed : kickInSpeed;
        _curEuler = Vector3.MoveTowards(_curEuler, _targetEuler, speed * dt);

        // 목표에 도달했으면 복귀 모드로
        if (!_returning && (_curEuler - _targetEuler).sqrMagnitude <= 0.0001f)
        {
            _returning = true;
            _targetEuler = Vector3.zero;
        }

        // 잔진동(복귀 중에만 살짝)
        Vector3 noise = Vector3.zero;
        if (_returning && _curEuler.sqrMagnitude > 0.00001f && noiseAmount > 0f)
        {
            float t = Time.time * noiseFreq + _noiseSeed;

            // -1~1
            float nx = Mathf.PerlinNoise(t, 0.13f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0.37f, t) * 2f - 1f;
            float nz = Mathf.PerlinNoise(t, 0.71f) * 2f - 1f;

            // 현재 흔들림 크기에 비례해서 줄어듦
            float amp = _curEuler.magnitude * noiseAmount;
            noise = new Vector3(nx, ny, nz) * amp;

            // 감쇠
            noise *= Mathf.Exp(-damping * dt);
        }

        Vector3 final = _curEuler + noise;

        // 축 마스크
        if (!affectPitch) final.x = 0f;
        if (!affectYaw) final.y = 0f;
        if (!affectRoll) final.z = 0f;

        transform.localRotation = _baseLocalRot * Quaternion.Euler(final);
    }

    /// <summary>
    /// 발사 반동 킥 시작. intensity=1 기준.
    /// </summary>
    public void TriggerKick(float intensity = 1f)
    {
        intensity = Mathf.Max(0f, intensity);

        float pitch = maxKickEuler.x * intensity;
        float yaw = maxKickEuler.y * intensity;
        float roll = maxKickEuler.z * intensity;

        // 발사 반동 느낌: 보통 pitch는 위로 살짝(카메라가 들리는 느낌) or 아래로도 가능
        // 현재는 랜덤 약간 섞고 pitch는 항상 같은 방향
        float yawSign = randomizeYawRoll ? (Random.value < 0.5f ? -1f : 1f) : 1f;
        float rollSign = randomizeYawRoll ? (Random.value < 0.5f ? -1f : 1f) : 1f;

        Vector3 kick = new Vector3(
            pitch,
            yaw * yawSign * Random.Range(0.6f, 1f),
            roll * rollSign * Random.Range(0.4f, 1f)
        );

        // 연사 시 기존 흔들림 위에 누적되게(너무 튀지 않게 clamp)
        _curEuler += kick * 0.35f;
        _curEuler = ClampVectorMagnitudePerAxis(_curEuler, maxKickEuler * 2.5f);

        _targetEuler = kick;
        _returning = false;
    }

    private static Vector3 ClampVectorMagnitudePerAxis(Vector3 v, Vector3 maxAbs)
    {
        v.x = Mathf.Clamp(v.x, -Mathf.Abs(maxAbs.x), Mathf.Abs(maxAbs.x));
        v.y = Mathf.Clamp(v.y, -Mathf.Abs(maxAbs.y), Mathf.Abs(maxAbs.y));
        v.z = Mathf.Clamp(v.z, -Mathf.Abs(maxAbs.z), Mathf.Abs(maxAbs.z));
        return v;
    }
}
