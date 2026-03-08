using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SoundController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] protected AudioSource normalSource;  // 효과음용 
    [SerializeField] protected AudioSource loopSource; //루프 전용


    [Header("Turret Traverse")]
    [SerializeField] private AudioSource turretAudioSource;
    [SerializeField] private AudioClip turretMovingClip; // 긴 루프용 클립
    [SerializeField] private float fadeSpeed = 3f;       // 정지 시 페이드 아웃 속도
    [SerializeField] private float targetVolume = 1f;

    private bool isMoving = false;

    public void IsTurretMoving(bool isMoving) => SetTurretMoving(isMoving);

    protected virtual void Awake()
    {
        turretAudioSource.clip = turretMovingClip;
        turretAudioSource.loop = true;
        turretAudioSource.volume = 0f;
        turretAudioSource.Play();
    }

    protected virtual void Update()
    {
        float target = isMoving ? targetVolume : 0f;
        turretAudioSource.volume = Mathf.MoveTowards(
            turretAudioSource.volume, target, fadeSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 터렛쪽 작동부
    /// </summary>
    private void SetTurretMoving(bool moving)
    {
        isMoving = moving;
    }

}
