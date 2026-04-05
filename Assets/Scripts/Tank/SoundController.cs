using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SoundController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] protected AudioSource normalSource;
    [SerializeField] protected AudioSource loopSource;
    [SerializeField] private float normalSourceMinDist = 200f;
    [SerializeField] private float normalSourceMaxDist = 3000f;

    [Header("Turret Traverse")]
    [SerializeField] private AudioSource turretAudioSource;
    [SerializeField] private AudioClip turretMovingClip;
    [SerializeField] private float fadeSpeed = 3f;
    [SerializeField] private float targetVolume = 1f;

    [Header("Gun Fire")]
    [SerializeField] private AudioClip[] gunFireClip;
    [SerializeField] private float gunFireVolume = 1f;

    [Header("Ammo Rack")]
    [SerializeField] private AudioClip[] ammoPop;
    [SerializeField] private float ammoPopVolume = 1.0f;
    [SerializeField] private AudioClip[] ammoExplosion;
    [SerializeField] private float ammoExplosionVolume = 1.0f;

    [Header("Fire Effect")]
    [SerializeField] private AudioClip fireLoopClip;
    [SerializeField] private float fireLoopVolume = 1f;
    private AudioSource fireLoopSource;

    private bool isMoving = false;

    public void IsTurretMoving(bool isMoving) => SetTurretMoving(isMoving);
    public void PlayGunFireClips() => PlayEffectSounds(gunFireClip, gunFireVolume);
    public void PlayAmmoExplosion() => PlayEffectSounds(ammoExplosion, ammoExplosionVolume);
    public void PlayAmmoPop() => PlayEffectSounds(ammoPop, ammoPopVolume);
    public void PlayFire() => StartFireLoop();
    public void StopFire() => StopFireLoop();

    private void StartFireLoop()
    {
        if (fireLoopSource == null)
            fireLoopSource = EnsureSourceExists(null, "Audio_FireLoop");

        if (fireLoopSource == null || fireLoopClip == null) return;
        if (fireLoopSource.isPlaying) return;
        fireLoopSource.clip   = fireLoopClip;
        fireLoopSource.loop   = true;
        fireLoopSource.volume = fireLoopVolume;
        fireLoopSource.spatialBlend = 1f;
        fireLoopSource.Play();
    }

    private void StopFireLoop()
    {
        if (fireLoopSource != null && fireLoopSource.isPlaying)
            fireLoopSource.Stop();
    }

    protected virtual void Awake()
    {
        EnsureDedicatedAudioSources();

        normalSource.spatialBlend = 1f;
        normalSource.rolloffMode  = AudioRolloffMode.Logarithmic;
        normalSource.minDistance  = normalSourceMinDist;
        normalSource.maxDistance  = normalSourceMaxDist;

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

    private void SetTurretMoving(bool moving)
    {
        isMoving = moving;
    }

    private void EnsureDedicatedAudioSources()
    {
        normalSource      = EnsureSourceExists(normalSource,      "Audio_Normal");
        loopSource        = EnsureSourceExists(loopSource,        "Audio_EngineLoop");
        turretAudioSource = EnsureSourceExists(turretAudioSource, "Audio_Turret");
        fireLoopSource    = EnsureSourceExists(fireLoopSource,    "Audio_FireLoop");
        fireLoopSource.loop = true;

        if (ReferenceEquals(loopSource, normalSource))
            loopSource = CreateDedicatedSource("Audio_EngineLoop_Auto", loopSource);

        if (ReferenceEquals(turretAudioSource, normalSource) || ReferenceEquals(turretAudioSource, loopSource))
            turretAudioSource = CreateDedicatedSource("Audio_Turret_Auto", turretAudioSource);

        if (loopSource != null)
            loopSource.loop = true;
    }

    private AudioSource EnsureSourceExists(AudioSource source, string childName)
    {
        if (source != null) return source;

        Transform child = transform.Find(childName);
        if (child == null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        var src = child.GetComponent<AudioSource>();
        if (src == null) src = child.gameObject.AddComponent<AudioSource>();
        return src;
    }

    private AudioSource CreateDedicatedSource(string childName, AudioSource template)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        var src = child.GetComponent<AudioSource>();
        if (src == null) src = child.gameObject.AddComponent<AudioSource>();

        if (template != null)
            CopyAudioSourceSettings(template, src);

        return src;
    }

    private static void CopyAudioSourceSettings(AudioSource from, AudioSource to)
    {
        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.mute = from.mute;
        to.bypassEffects = from.bypassEffects;
        to.bypassListenerEffects = from.bypassListenerEffects;
        to.bypassReverbZones = from.bypassReverbZones;
        to.playOnAwake = from.playOnAwake;
        to.loop = from.loop;
        to.priority = from.priority;
        to.volume = from.volume;
        to.pitch = from.pitch;
        to.panStereo = from.panStereo;
        to.spatialBlend = from.spatialBlend;
        to.reverbZoneMix = from.reverbZoneMix;
        to.dopplerLevel = from.dopplerLevel;
        to.spread = from.spread;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;
        to.rolloffMode = from.rolloffMode;
    }


    /// <summary>
    /// true: 음속 딜레이 적용 (적 탱크)
    /// false: 즉시 재생 (플레이어 - 딜레이 불필요)
    /// </summary>
    protected virtual bool UseSpeedOfSound => true;

    protected void PlayEffectSounds(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0 || normalSource == null) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (UseSpeedOfSound && PoolManager.Instance != null)
        {
            float delay = PoolManager.Instance.GetSoundDelay(transform.position);
            if (delay < 0.02f)
                normalSource.PlayOneShot(clip, volume);
            else
                StartCoroutine(CoPlayDelayed(clip, volume, delay));
        }
        else
        {
            normalSource.PlayOneShot(clip, volume);
        }
    }

    private IEnumerator CoPlayDelayed(AudioClip clip, float volume, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (normalSource != null)
            normalSource.PlayOneShot(clip, volume);
    }
}
