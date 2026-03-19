using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System;
using UnityEngine.Experimental.GlobalIllumination;

public class PlayerTankSoundController : SoundController
{
    private enum EngineLoopState
    {
        Idle,
        RpmLow,
        RpmSpeed
    }

    [Header("Audio")]
    [SerializeField] private AudioSource radioSource;

    [Header("Engine")]
    [SerializeField] private DriverController driverController;
    [SerializeField] private ModuleManager moduleManager;
    [SerializeField] private AudioSource engineOneShotSource;
    [SerializeField] private AudioClip engineStartClip;
    [SerializeField] private AudioClip engineFailureClip;
    [SerializeField] private AudioClip engineIdleClip;
    [SerializeField] private AudioClip engineRpmLowClip;
    [SerializeField] private AudioClip engineRpmSpeedClip;
    [SerializeField] private AudioClip engineThrottleClip;
    [SerializeField, Range(0f, 1f)] private float throttleTriggerThreshold = 0.35f;
    [SerializeField] private float throttleCooldown = 0.2f;
    [SerializeField, Range(0f, 1f)] private float throttleClipGateRatio = 0.85f;
    [SerializeField] private float idleMaxSpeed = 0.15f;
    [SerializeField] private float lowMaxSpeed = 2.5f;
    [SerializeField] private float pivotThreshold = 0.2f;
    [SerializeField] private float steerPivotThreshold = 0.2f;
    [SerializeField] private float inputDeadZone = 0.05f;
    [SerializeField] private float idleForceDelay = 0.02f;
    [SerializeField] private bool forceCutToIdleOnNoInput = true;
    [SerializeField] private float forceIdleCutMaxSpeed = 0.8f;
    [SerializeField] private float engineCrossfadeTime = 0.2f;
    [SerializeField] private float startToIdleFadeTime = 0.3f;
    [SerializeField] private float startIdlePrerollTime = 0.25f;
    [SerializeField] private bool debugEngineState = false;

    [Header("Radio Effect")]
    [SerializeField] private AudioClip[] radioOn;
    [SerializeField] private float radioOnVolume;
    [SerializeField] private AudioClip[] radioOff;
    [SerializeField] private float radioOffVolume;
    [SerializeField] private AudioMixer radioMixer;
    [SerializeField] private AudioMixerSnapshot normalSnapshot;
    [SerializeField] private AudioMixerSnapshot radioSnapshot;
    [SerializeField] private float transitionTime = 0.05f;

    [Header("Crew Commander")]
    [SerializeField] private AudioClip[] loadAP;
    [SerializeField] private AudioClip[] loadHE;
    [SerializeField] private AudioClip[] enemyInSightCrewClip;

    [Header("Crew Loader")]
    [SerializeField] private AudioClip[] reloadCrewClip;

    [Header("Crew Gunner")]
    [SerializeField] private AudioClip[] targetDownCrewClip;
    [SerializeField] private AudioClip[] fireCrewClip;
    [SerializeField] private AudioClip[] tankPenetrationCrewClip;
    [SerializeField] private AudioClip[] tankNonePenetrationCrewClip;
    [SerializeField] private AudioClip[] tankRicochetCrewClip;

    [Header("Crew Subtitles")]
    [SerializeField] private bool useCrewSubtitles = true;
    [SerializeField] private CrewSubtitleManager subtitleManager;

    [Header("Startup Sequence")]
    [SerializeField] private AudioClip commanderReadyClip;
    [SerializeField] private AudioClip driverReadyClip;
    [SerializeField] private AudioClip gunnerReadyClip;
    [SerializeField] private AudioClip loaderReadyClip;
    [SerializeField] private float clipGap = 0.5f;
    [SerializeField] private float crewVoiceStartDelay = 0.15f;

    [Header("FlyBy")]
    [SerializeField] private float flyByRadius = 8f;
    [SerializeField] private LayerMask shellMask;
    [SerializeField] private AudioClip[] flyByClips;
    [SerializeField] private float flyByVolume = 1f;
    [SerializeField] private float flyByCooldown = 0.3f;

    [Header("Reload")]
    [SerializeField] private AudioClip[] reloadClips;
    [SerializeField] private float reloadVolume = 1f;

    private float cooldownTimer = 0f;
    private float throttleCooldownTimer = 0f;
    private float prevForwardThrottle = 0f;
    private float noDriveInputTimer = 0f;
    private bool idleForcedWhileNoInput = false;
    private bool engineLoopArmed = false;
    private bool startupIdleTransition = false;
    private EngineLoopState currentEngineLoopState = EngineLoopState.Idle;

    [Header("Engine Audio Source")]
    private AudioSource engineLoopA;
    private AudioSource engineLoopB;
    private AudioSource activeEngineLoop;
    private AudioSource inactiveEngineLoop;
    private Coroutine crossfadeRoutine;

    private ModuleDamageController engineModule;
    private readonly HashSet<string> busySpeakers = new HashSet<string>();
    private bool engineDestroyed = false;

    public void PlayReload() => PlayEffectSounds(reloadClips, reloadVolume);
    public void PlayFireCrewVoice() => PlayCrewVoicesWithRadio(fireCrewClip, 1f, CrewSubtitleCue.Fire);
    public void PlayReloadCrewVoice() => PlayCrewVoicesWithRadio(reloadCrewClip, 1f, CrewSubtitleCue.Reload);
    public void PlayTargetDown() => PlayCrewVoicesWithRadio(targetDownCrewClip, 1f, CrewSubtitleCue.TargetDown);
    public void PlayAPLoad() => PlayCrewVoicesWithRadio(loadAP, flyByVolume, CrewSubtitleCue.LoadAP);
    public void PlayHELoad() => PlayCrewVoicesWithRadio(loadHE, flyByVolume, CrewSubtitleCue.LoadHE);
    public void PlayPenetratedCrewVoice()=> PlayCrewVoicesWithRadio(tankPenetrationCrewClip, 1f, CrewSubtitleCue.Penetrated);
    public void PlayNonePenetratedCrewVoice() => PlayCrewVoicesWithRadio(tankNonePenetrationCrewClip, 1f, CrewSubtitleCue.NoPenetration);
    public void PlayRicochetCrewVoice() => PlayCrewVoicesWithRadio(tankRicochetCrewClip, 1f, CrewSubtitleCue.Ricocheted);
    public void PlayInSightCrewVoice() =>  PlayCrewVoicesWithRadio(enemyInSightCrewClip, 1f, CrewSubtitleCue.EnemyInSight);

    private void OnEnable()
    {
        BallisticManager.OnEnemyPenetrated += PlayPenetratedCrewVoice;
        BallisticManager.OnEnemyNoPenetration += PlayNonePenetratedCrewVoice;
        BallisticManager.OnEnemyRicocheted += PlayRicochetCrewVoice;
    }
    private void OnDisable()
    {
        BallisticManager.OnEnemyPenetrated -= PlayPenetratedCrewVoice;
        BallisticManager.OnEnemyNoPenetration -= PlayNonePenetratedCrewVoice;
        BallisticManager.OnEnemyRicocheted -= PlayRicochetCrewVoice;
    }
    protected override void Awake()
    {
        base.Awake();

        if (driverController == null)
            driverController = GetComponent<DriverController>();

        if (engineOneShotSource == null)
            engineOneShotSource = normalSource;

        if (subtitleManager == null)
            subtitleManager = FindAnyObjectByType<CrewSubtitleManager>();

        SetupEngineLoopSources();

        if (activeEngineLoop == null)
            Debug.LogWarning("[EngineSound] loopSource is not assigned.", this);
        if (engineIdleClip == null)
            Debug.LogWarning("[EngineSound] engineIdleClip is not assigned.", this);

        LogLoopSourceDiagnostics("Awake");
    }

    private void Start()
    {
        if (moduleManager == null) moduleManager = GetComponent<ModuleManager>();
        if (moduleManager != null)
        {
            engineModule = moduleManager.GetCrew(ModuleType.Engine);
            if (engineModule != null)
                engineModule.OnStateChanged += OnEngineStateChanged;
        }

        float startDelay = 0f;
        if (engineStartClip != null && engineOneShotSource != null)
        {
            engineOneShotSource.PlayOneShot(engineStartClip);
            startDelay = engineStartClip.length;
        }
        else if (debugEngineState)
        {
            Debug.Log("[EngineSound] Start clip or one-shot source missing. Arming loop immediately.", this);
        }

        StartCoroutine(ArmEngineLoopAfterStart(startDelay));
        StartCoroutine(PlayStartupSequence());
    }

    protected override void Update()
    {
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            var cols = Physics.OverlapSphere(transform.position, flyByRadius, shellMask);
            foreach (var col in cols)
            {
                var shell = col.GetComponent<BallisticManager>();
                if (shell != null && shell.isPlayerShell) continue;

                PlayEffectSounds(flyByClips, flyByVolume);
                cooldownTimer = flyByCooldown;
                break;
            }
        }

        UpdateEngineLoop();
        base.Update();
    }

    private IEnumerator ArmEngineLoopAfterStart(float startDelay)
    {
        float waitBeforeFade = Mathf.Max(0f, startDelay - startIdlePrerollTime);
        if (waitBeforeFade > 0f)
            yield return new WaitForSeconds(waitBeforeFade);

        float overlapDuration = Mathf.Max(0.01f, startDelay - waitBeforeFade);
        float fadeDuration = Mathf.Max(startToIdleFadeTime, overlapDuration);

        if (debugEngineState)
            Debug.Log($"[EngineSound] Start->Idle overlap begin (wait={waitBeforeFade:0.00}s, fade={fadeDuration:0.00}s)", this);

        yield return FadeInIdleFromStart(fadeDuration);

        engineLoopArmed = true;
        if (debugEngineState)
            Debug.Log("[EngineSound] Start->Idle transition complete. Engine loop armed.", this);
    }

    private void UpdateEngineLoop()
    {
        if (engineDestroyed) return;
        if (!engineLoopArmed || driverController == null) return;

        float currentSpeed = Mathf.Abs(driverController.CurrentSpeed);
        float targetThrottle = driverController.TargetThrottle;
        float targetPivot = driverController.TargetPivot;
        float targetSteer = driverController.TargetSteer;
        float forwardThrottle = Mathf.Clamp01(targetThrottle);

        bool hasCommandInput =
            Mathf.Abs(targetThrottle) >= inputDeadZone ||
            Mathf.Abs(targetPivot) >= inputDeadZone ||
            Mathf.Abs(targetSteer) >= inputDeadZone;

        if (hasCommandInput) noDriveInputTimer = 0f;
        else noDriveInputTimer += Time.deltaTime;

        if (hasCommandInput) idleForcedWhileNoInput = false;

        if (!startupIdleTransition && forceCutToIdleOnNoInput && !hasCommandInput && noDriveInputTimer >= idleForceDelay && currentSpeed <= forceIdleCutMaxSpeed)
        {
            if (!idleForcedWhileNoInput)
            {
                ForceIdleLoopHard();
                idleForcedWhileNoInput = true;
            }
            return;
        }

        throttleCooldownTimer -= Time.deltaTime;
        if (engineThrottleClip != null && engineOneShotSource != null)
        {
            bool throttleEdge = forwardThrottle >= throttleTriggerThreshold && prevForwardThrottle < throttleTriggerThreshold;
            bool sourceBusy = engineOneShotSource.isPlaying;
            if (throttleEdge && throttleCooldownTimer <= 0f && !sourceBusy)
            {
                engineOneShotSource.PlayOneShot(engineThrottleClip);
                float clipGate = engineThrottleClip.length * throttleClipGateRatio;
                throttleCooldownTimer = Mathf.Max(throttleCooldown, clipGate);
            }
        }
        prevForwardThrottle = forwardThrottle;

        bool isPivoting = Mathf.Abs(targetPivot) >= pivotThreshold ||
                          (Mathf.Abs(targetSteer) >= steerPivotThreshold && Mathf.Abs(targetThrottle) < inputDeadZone && currentSpeed <= lowMaxSpeed);
        bool isReversing = targetThrottle < -inputDeadZone;
        bool isStopped = currentSpeed <= idleMaxSpeed && !hasCommandInput;

        EngineLoopState nextState;
        if (isPivoting)
            nextState = EngineLoopState.RpmSpeed;
        else if (isReversing)
            nextState = EngineLoopState.RpmLow;
        else if (isStopped)
            nextState = EngineLoopState.Idle;
        else if (currentSpeed < lowMaxSpeed)
            nextState = EngineLoopState.RpmLow;
        else
            nextState = EngineLoopState.RpmSpeed;

        SetEngineLoopState(nextState);
    }

    private void ForceIdleLoopHard()
    {
        if (crossfadeRoutine != null)
        {
            StopCoroutine(crossfadeRoutine);
            crossfadeRoutine = null;
        }

        if (engineOneShotSource != null && engineOneShotSource.isPlaying)
            engineOneShotSource.Stop();

        if (engineLoopA != null) engineLoopA.Stop();
        if (engineLoopB != null) engineLoopB.Stop();

        if (activeEngineLoop != null) activeEngineLoop.volume = 1f;
        if (inactiveEngineLoop != null) inactiveEngineLoop.volume = 0f;

        SetEngineLoopState(EngineLoopState.Idle, forceHardSwitch: true);

        if (debugEngineState)
            Debug.Log("[EngineSound] Force cut -> Idle (ABSOLUTE)", this);
    }

    private IEnumerator FadeInIdleFromStart(float fadeDuration)
    {
        if (activeEngineLoop == null || engineIdleClip == null)
        {
            ForceIdleLoopHard();
            yield break;
        }

        startupIdleTransition = true;

        if (crossfadeRoutine != null)
        {
            StopCoroutine(crossfadeRoutine);
            crossfadeRoutine = null;
        }

        if (engineLoopA != null) engineLoopA.Stop();
        if (engineLoopB != null) engineLoopB.Stop();

        activeEngineLoop.clip = engineIdleClip;
        activeEngineLoop.volume = 0f;
        activeEngineLoop.Play();
        currentEngineLoopState = EngineLoopState.Idle;

        float duration = Mathf.Max(0.01f, fadeDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            activeEngineLoop.volume = Mathf.Clamp01(t / duration);
            yield return null;
        }

        activeEngineLoop.volume = 1f;
        startupIdleTransition = false;
    }

    private void SetEngineLoopState(EngineLoopState nextState, bool forceHardSwitch = false, float? crossfadeOverride = null)
    {
        if (activeEngineLoop == null || inactiveEngineLoop == null)
            return;

        if (currentEngineLoopState == nextState && activeEngineLoop.isPlaying)
            return;

        currentEngineLoopState = nextState;

        AudioClip nextClip = GetLoopClip(nextState);
        if (nextClip == null)
        {
            if (debugEngineState)
                Debug.LogWarning($"[EngineSound] Missing clip for state={nextState}", this);
            return;
        }

        if (crossfadeRoutine != null)
        {
            StopCoroutine(crossfadeRoutine);
            crossfadeRoutine = null;
        }

        float crossfadeTime = crossfadeOverride ?? engineCrossfadeTime;
        if (forceHardSwitch || crossfadeTime <= 0f || !activeEngineLoop.isPlaying)
        {
            activeEngineLoop.Stop();
            activeEngineLoop.clip = nextClip;
            activeEngineLoop.volume = 1f;
            activeEngineLoop.Play();
            if (debugEngineState)
            {
                Debug.Log($"[EngineSound] Loop -> {nextState} (hard switch)", this);
                LogLoopSourceDiagnostics("After Hard Switch");
            }
            return;
        }

        inactiveEngineLoop.Stop();
        inactiveEngineLoop.clip = nextClip;
        inactiveEngineLoop.volume = 0f;
        inactiveEngineLoop.Play();

        crossfadeRoutine = StartCoroutine(CrossfadeEngineLoops(activeEngineLoop, inactiveEngineLoop, crossfadeTime, nextState));
    }

    private IEnumerator CrossfadeEngineLoops(AudioSource from, AudioSource to, float duration, EngineLoopState targetState)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            from.volume = 1f - k;
            to.volume = k;
            yield return null;
        }

        from.Stop();
        from.volume = 1f;
        to.volume = 1f;

        SwapActiveLoopSources();
        crossfadeRoutine = null;

        if (debugEngineState)
        {
            Debug.Log($"[EngineSound] Loop -> {targetState} (crossfade)", this);
            LogLoopSourceDiagnostics("After Crossfade");
        }
    }

    private void SetupEngineLoopSources()
    {
        engineLoopA = loopSource;
        if (engineLoopA != null)
        {
            engineLoopA.loop = true;
            engineLoopA.playOnAwake = false;
            engineLoopA.volume = 1f;
        }

        engineLoopB = EnsureSecondaryLoopSource(engineLoopA);
        if (engineLoopB != null)
        {
            engineLoopB.loop = true;
            engineLoopB.playOnAwake = false;
            engineLoopB.volume = 0f;
        }

        activeEngineLoop = engineLoopA;
        inactiveEngineLoop = engineLoopB;
    }

    private AudioSource EnsureSecondaryLoopSource(AudioSource template)
    {
        if (template == null) return null;

        Transform child = transform.Find("Audio_EngineLoop_Blend");
        if (child == null)
        {
            var go = new GameObject("Audio_EngineLoop_Blend");
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        var src = child.GetComponent<AudioSource>();
        if (src == null) src = child.gameObject.AddComponent<AudioSource>();

        src.outputAudioMixerGroup = template.outputAudioMixerGroup;
        src.mute = template.mute;
        src.bypassEffects = template.bypassEffects;
        src.bypassListenerEffects = template.bypassListenerEffects;
        src.bypassReverbZones = template.bypassReverbZones;
        src.priority = template.priority;
        src.pitch = template.pitch;
        src.panStereo = template.panStereo;
        src.spatialBlend = template.spatialBlend;
        src.reverbZoneMix = template.reverbZoneMix;
        src.dopplerLevel = template.dopplerLevel;
        src.spread = template.spread;
        src.minDistance = template.minDistance;
        src.maxDistance = template.maxDistance;
        src.rolloffMode = template.rolloffMode;

        return src;
    }

    private void SwapActiveLoopSources()
    {
        AudioSource tmp = activeEngineLoop;
        activeEngineLoop = inactiveEngineLoop;
        inactiveEngineLoop = tmp;
        loopSource = activeEngineLoop;
    }

    private void LogLoopSourceDiagnostics(string phase)
    {
        if (!debugEngineState) return;
        if (activeEngineLoop == null)
        {
            Debug.LogWarning($"[EngineSound][{phase}] activeLoop=NULL", this);
            return;
        }

        string groupName = activeEngineLoop.outputAudioMixerGroup != null ? activeEngineLoop.outputAudioMixerGroup.name : "None";
        string mixerName = activeEngineLoop.outputAudioMixerGroup != null && activeEngineLoop.outputAudioMixerGroup.audioMixer != null
            ? activeEngineLoop.outputAudioMixerGroup.audioMixer.name
            : "None";
        string clipName = activeEngineLoop.clip != null ? activeEngineLoop.clip.name : "None";

        Debug.Log(
            $"[EngineSound][{phase}] playing={activeEngineLoop.isPlaying}, mute={activeEngineLoop.mute}, vol={activeEngineLoop.volume:0.00}, pitch={activeEngineLoop.pitch:0.00}, spatial={activeEngineLoop.spatialBlend:0.00}, loop={activeEngineLoop.loop}, clip={clipName}, group={groupName}, mixer={mixerName}",
            this);
    }

    private AudioClip GetLoopClip(EngineLoopState state)
    {
        return state switch
        {
            EngineLoopState.RpmLow => engineRpmLowClip,
            EngineLoopState.RpmSpeed => engineRpmSpeedClip,
            _ => engineIdleClip
        };
    }

    private void OnDestroy()
    {
        if (engineModule != null)
            engineModule.OnStateChanged -= OnEngineStateChanged;
    }

    private void OnEngineStateChanged(ModuleDamageController who, ModuleState prev, ModuleState next)
    {
        if (next == ModuleState.Destroyed)
        {
            engineDestroyed = true;
            StopAllEngineSounds();
            if (engineFailureClip != null && engineOneShotSource != null)
                engineOneShotSource.PlayOneShot(engineFailureClip);
        }
        else if (prev == ModuleState.Destroyed)
        {
            engineDestroyed = false;
            RestartEngine();
        }
    }

    private void StopAllEngineSounds()
    {
        engineLoopArmed = false;
        if (crossfadeRoutine != null) { StopCoroutine(crossfadeRoutine); crossfadeRoutine = null; }
        if (engineLoopA != null) engineLoopA.Stop();
        if (engineLoopB != null) engineLoopB.Stop();
        if (engineOneShotSource != null) engineOneShotSource.Stop();
    }

    private void RestartEngine()
    {
        StopAllEngineSounds();

        float startDelay = 0f;
        if (engineStartClip != null && engineOneShotSource != null)
        {
            engineOneShotSource.PlayOneShot(engineStartClip);
            startDelay = engineStartClip.length;
        }
        StartCoroutine(ArmEngineLoopAfterStart(startDelay));
    }

    private IEnumerator PlayStartupSequence()
    {
        yield return PlayAndWait(commanderReadyClip, CrewSubtitleCue.CommanderReady);
        yield return PlayAndWait(driverReadyClip, CrewSubtitleCue.DriverReady);
        yield return PlayAndWait(gunnerReadyClip, CrewSubtitleCue.GunnerReady);
        yield return PlayAndWait(loaderReadyClip, CrewSubtitleCue.LoaderReady);
    }

    private IEnumerator PlayAndWait(AudioClip clip, CrewSubtitleCue cue = CrewSubtitleCue.None)
    {
        if (clip == null) yield break;
        yield return PlayCrewVoiceWithRadioRoutine(clip, 1f, clipGap, cue);
    }

    public void PlayCrewVoiceWithRadio(AudioClip clip, float voiceVolume = 1f)
    {
        if (clip == null) return;
        StartCoroutine(PlayCrewVoiceWithRadioRoutine(clip, voiceVolume, 0f, CrewSubtitleCue.None));
    }

    public void PlayCrewVoiceWithRadio(AudioClip clip, float voiceVolume, CrewSubtitleCue cue)
    {
        if (clip == null) return;
        StartCoroutine(PlayCrewVoiceWithRadioRoutine(clip, voiceVolume, 0f, cue));
    }

    public void PlayCrewVoicesWithRadio(AudioClip[] clips, float voiceVolume, CrewSubtitleCue cue)
    {
        string speaker = GetSpeaker(cue);
        if (speaker != null && busySpeakers.Contains(speaker)) return;
        AudioClip clip = GetRandomClip(clips);
        if (clip == null) return;
        StartCoroutine(PlayCrewVoiceWithRadioRoutine(clip, voiceVolume, 0f, cue, speaker));
    }

    private static string GetSpeaker(CrewSubtitleCue cue)
    {
        switch (cue)
        {
            case CrewSubtitleCue.Fire:
            case CrewSubtitleCue.Penetrated:
            case CrewSubtitleCue.NoPenetration:
            case CrewSubtitleCue.Ricocheted:
            case CrewSubtitleCue.TargetDown:
                return "gunner";
            case CrewSubtitleCue.LoadAP:
            case CrewSubtitleCue.LoadHE:
            case CrewSubtitleCue.EnemyInSight:
            case CrewSubtitleCue.CommanderReady:
                return "commander";
            case CrewSubtitleCue.Reload:
            case CrewSubtitleCue.LoaderReady:
                return "loader";
            case CrewSubtitleCue.DriverReady:
                return "driver";
            default:
                return null;
        }
    }

    private static AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    private IEnumerator PlayCrewVoiceWithRadioRoutine(AudioClip voiceClip, float voiceVolume, float extraGapAfter, CrewSubtitleCue cue, string speaker = null)
    {
        if (voiceClip == null) yield break;

        if (speaker != null) busySpeakers.Add(speaker);

        if (radioSnapshot != null)
            radioSnapshot.TransitionTo(transitionTime);

        AudioClip onClip = GetRandomClip(radioOn);
        if (onClip != null && normalSource != null)
        {
            normalSource.PlayOneShot(onClip, radioOnVolume);
            yield return new WaitForSeconds(onClip.length);
        }

        if (crewVoiceStartDelay > 0f)
            yield return new WaitForSeconds(crewVoiceStartDelay);

        ShowSubtitle(cue, voiceClip.length);

        if (radioSource != null)
            radioSource.PlayOneShot(voiceClip, voiceVolume);
        else if (normalSource != null)
            normalSource.PlayOneShot(voiceClip, voiceVolume);

        yield return new WaitForSeconds(voiceClip.length);

        AudioClip offClip = GetRandomClip(radioOff);
        if (offClip != null && normalSource != null)
        {
            normalSource.PlayOneShot(offClip, radioOffVolume);
            yield return new WaitForSeconds(offClip.length);
        }

        if (normalSnapshot != null)
            normalSnapshot.TransitionTo(transitionTime);

        if (extraGapAfter > 0f)
            yield return new WaitForSeconds(extraGapAfter);

        if (speaker != null) busySpeakers.Remove(speaker);
    }

    private void ShowSubtitle(CrewSubtitleCue cue, float duration)
    {
        if (!useCrewSubtitles || cue == CrewSubtitleCue.None) return;
        if (subtitleManager == null) subtitleManager = FindAnyObjectByType<CrewSubtitleManager>();

        if (subtitleManager != null)
        {
            subtitleManager.Show(cue, duration);

        }
            
    }

}
