using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetForwardMoverAdvanced : MonoBehaviour
{
    [Header("Base Move")]
    [SerializeField] private float speed = 8f;  // m/s

    [Header("Sway (optional)")]
    [SerializeField] private bool enableSway = true;
    [SerializeField] private float swayAmplitude = 2f;  // meters (좌우 폭)
    [SerializeField] private float swayFrequency = 0.5f; // Hz

    [Header("Yaw Turn (optional)")]
    [SerializeField] private bool enableYawTurn = false;
    [SerializeField] private float yawDegPerSec = 10f;

    private Vector3 _startPos;
    private float _t;

    void Start()
    {
        _startPos = transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;

        if (enableYawTurn)
        {
            transform.Rotate(0f, yawDegPerSec * dt, 0f, Space.World);
        }

        // 전진
        transform.position += transform.forward * speed * dt;

        // 좌우 흔들림(월드 기준 right로)
        if (enableSway)
        {
            float offset = Mathf.Sin(_t * Mathf.PI * 2f * swayFrequency) * swayAmplitude;
            // 현재 위치에서 right방향으로 스웨이만 추가
            transform.position += transform.right * (offset * dt * swayFrequency);
        }
    }
}
