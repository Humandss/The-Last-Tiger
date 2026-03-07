using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEngine;

public class ShellSoundController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Sounds")]
    [SerializeField] private AudioClip[] hitClips;
    [SerializeField] private AudioClip[] ricochetClips;
    [SerializeField] private AudioClip[] explosionClips;

    [Header("Volume")]
    [SerializeField] private float hitVolume = 1f;
    [SerializeField] private float ricochetVolume = 1f;
    [SerializeField] private float explosionVolume = 1f;

    public void PlayHit(Vector3 pos) => PlayRandom(hitClips, pos, hitVolume);
    public void PlayRicochet(Vector3 pos) => PlayRandom(ricochetClips, pos, ricochetVolume);

    private void PlayRandom(AudioClip[] clips, Vector3 pos, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        AudioSource.PlayClipAtPoint(clip, pos, volume);
    }


}
