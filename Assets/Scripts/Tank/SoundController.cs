using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SoundController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] protected AudioSource normalSource;
    [SerializeField] protected AudioSource loopSource;

    [Header("Turret Traverse")]
    [SerializeField] private AudioSource turretAudioSource;
    [SerializeField] private AudioClip turretMovingClip;
    [SerializeField] private float fadeSpeed = 3f;
    [SerializeField] private float targetVolume = 1f;

    private bool isMoving = false;

    public void IsTurretMoving(bool isMoving) => SetTurretMoving(isMoving);

    protected virtual void Awake()
    {
        EnsureDedicatedAudioSources();

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
        normalSource = EnsureSourceExists(normalSource, "Audio_Normal");
        loopSource = EnsureSourceExists(loopSource, "Audio_EngineLoop");
        turretAudioSource = EnsureSourceExists(turretAudioSource, "Audio_Turret");

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
}
