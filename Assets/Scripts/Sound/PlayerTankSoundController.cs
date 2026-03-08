using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class PlayerTankSoundController : SoundController
{
    [Header("Audio")]
    [SerializeField] private AudioSource radioSource;   // 무전음용 

    [Header("Radio Effect")]
    [SerializeField] private AudioMixer radioMixer;
    [SerializeField] private AudioMixerSnapshot normalSnapshot;
    [SerializeField] private AudioMixerSnapshot radioSnapshot;
    [SerializeField] private float transitionTime = 0.05f;

    [Header("Voice Clips")]
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip reloadClip;
    [SerializeField] private AudioClip targetDownClip;


    [Header("Startup Sequence")]
    [SerializeField] private AudioClip commanderReadyClip; 
    [SerializeField] private AudioClip driverReadyClip;    
    [SerializeField] private AudioClip gunnerReadyClip;    
    [SerializeField] private AudioClip loaderReadyClip;     
    [SerializeField] private float clipGap = 0.5f;          // 클립 사이 간격

    [Header("Gun Fire")]
    [SerializeField] private AudioClip[] gunFireClip;
    [SerializeField] private float gunFireVolume = 1f;

    [Header("FlyBy")]
    [SerializeField] private float flyByRadius = 8f;
    [SerializeField] private LayerMask shellMask; // 탄 레이어
    [SerializeField] private AudioClip[] flyByClips;
    [SerializeField] private float flyByVolume = 1f;
    [SerializeField] private float flyByCooldown = 0.3f; // 연속 재생 방지
    private float cooldownTimer = 0f;

    public void PlayGunFireClips() => PlayEffectSounds(gunFireClip, gunFireVolume);
    public void PlayReload() => PlayCrewVoice(reloadClip);
    public void PlayTargetDown() => PlayCrewVoice(targetDownClip);

    protected override void Awake()
    {
        base.Awake(); 
    }

    private void Start()
    {
        StartCoroutine(PlayStartupSequence());
    }
    protected override void Update()
    {
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer > 0f) return;

        // 주변 탄 감지
        var cols = Physics.OverlapSphere(transform.position, flyByRadius, shellMask);
        foreach (var col in cols)
        {
            var shell = col.GetComponent<BallisticManager>();
            if (shell != null && shell.isPlayerShell) continue; // 플레이어 탄 무시

            PlayEffectSounds(flyByClips, flyByVolume);
            cooldownTimer = flyByCooldown;
            break;
        }

        base.Update();

    }
    private IEnumerator PlayStartupSequence()
    {
        yield return PlayAndWait(commanderReadyClip);
        yield return PlayAndWait(driverReadyClip);
        yield return PlayAndWait(gunnerReadyClip);
        yield return PlayAndWait(loaderReadyClip);
    }

    private IEnumerator PlayAndWait(AudioClip clip)
    {
        if (clip == null || radioSource == null) yield break;

        radioSnapshot.TransitionTo(transitionTime);
        radioSource.PlayOneShot(clip);
        yield return new WaitForSeconds(clip.length + clipGap);
        normalSnapshot.TransitionTo(transitionTime);   
    }

    private void PlayCrewVoice(AudioClip clip)
    {
        if (clip == null || radioSource == null) return;
        radioSource.PlayOneShot(clip);
    }

    private void PlayEffectSounds(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        normalSource.PlayOneShot(clip, volume); 
    }

 
}
