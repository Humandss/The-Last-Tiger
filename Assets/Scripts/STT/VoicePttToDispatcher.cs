using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;

public class VoicePttToDispatcher : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CrewCommandDispatcher dispatcher;
    [SerializeField] private MonoBehaviour sttRunnerBehaviour; // ISttRunner 구현체를 넣기

    [Header("PTT")]
    [SerializeField] private KeyCode pttKey = KeyCode.V;
    [SerializeField] private int preRollMs = 300;
    [SerializeField] private int postRollMs = 350;

    [Header("Mic")]
    [SerializeField] private string deviceName = "";
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int micLoopSeconds = 8;
    [SerializeField] private bool autoStartMicOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    [SerializeField] private bool saveDebugWav = true;

    private ISttRunner sttRunner;

    private AudioClip micClip;
    private bool micReady;

    private bool isPttPressed;
    private bool isFinalizing;

    private int pttStartFrame;       // pre-roll 포함 시작 frame
    private int postRollRemainFrames;
    private int lastMicPos;

    private void Awake()
    {
        sttRunner = sttRunnerBehaviour as ISttRunner;

        if (sttRunnerBehaviour != null && sttRunner == null)
        {
            Debug.LogError("[VoicePTT] sttRunnerBehaviour가 ISttRunner를 구현하지 않았음.");
        }

        if (dispatcher == null)
        {
            Debug.LogError("[VoicePTT] dispatcher 참조 필요.");
        }
    }

    private void OnEnable()
    {
        if (autoStartMicOnEnable)
            StartMic();
    }

    private void OnDisable()
    {
        StopMic();
    }

    private void Update()
    {
        if (!micReady) return;

        int micPos = Microphone.GetPosition(GetDeviceOrNull());
        if (micPos < 0) return;

        // V Down -> 녹음 시작 (pre-roll 포함 시작점 기록)
        if (!isFinalizing && Input.GetKeyDown(pttKey))
        {
            if (!isPttPressed)
            {
                isPttPressed = true;

                int preFrames = MsToFrames(preRollMs);
                pttStartFrame = WrapFrame(micPos - preFrames, micClip.samples);

                if (debugLog)
                    Debug.Log($"[VoicePTT] KEY DOWN, micPos={micPos}, start(pre)={pttStartFrame}");
            }
        }

        // V Up -> post-roll 시작
        if (isPttPressed && Input.GetKeyUp(pttKey))
        {
            isPttPressed = false;
            isFinalizing = true;
            postRollRemainFrames = MsToFrames(postRollMs);

            if (debugLog)
                Debug.Log($"[VoicePTT] KEY UP, micPos={micPos}, postRollFrames={postRollRemainFrames}");
        }

        // post-roll 진행 중이면 마이크 진행량만큼 감소
        if (isFinalizing)
        {
            int advanced = GetAdvancedFrames(lastMicPos, micPos, micClip.samples);
            postRollRemainFrames -= advanced;

            if (postRollRemainFrames <= 0)
            {
                int finalPos = Microphone.GetPosition(GetDeviceOrNull());
                if (finalPos < 0) finalPos = micPos;

                isFinalizing = false;

                float[] samples = ExtractSegmentCircular(micClip, pttStartFrame, finalPos, 1);
                if (samples == null || samples.Length == 0)
                {
                    if (debugLog) Debug.LogWarning("[VoicePTT] 캡처 샘플 길이 0");
                }
                else
                {
                    StartCoroutine(ProcessCapturedAudio(samples));
                }
            }
        }

        lastMicPos = micPos;
    }

    private IEnumerator ProcessCapturedAudio(float[] monoSamples)
    {
        if (debugLog)
        {
            float sec = monoSamples.Length / (float)sampleRate;
            Debug.Log($"[VoicePTT] Captured {monoSamples.Length} samples ({sec:0.000}s)");
        }

        // 너무 짧은 발화 방지 (노이즈 클릭/오입력 컷)
        float durationSec = monoSamples.Length / (float)sampleRate;
        if (durationSec < 0.15f)
        {
            if (debugLog) Debug.Log("[VoicePTT] 너무 짧아서 무시");
            yield break;
        }

        byte[] wavBytes = WavUtility.FromMonoFloat(monoSamples, sampleRate);

        if (saveDebugWav)
        {
            string path = Path.Combine(Application.persistentDataPath, "ptt_debug.wav");
            File.WriteAllBytes(path, wavBytes);
            if (debugLog) Debug.Log($"[VoicePTT] WAV saved: {path}");
        }

        // STT 엔진 연결 안 되어 있으면 디버그용 종료
        if (sttRunner == null)
        {
            if (debugLog) Debug.LogWarning("[VoicePTT] STT Runner 없음. Inspector에 ISttRunner 구현체 연결해.");
            yield break;
        }

        bool done = false;
        string sttText = null;
        string sttError = null;

        yield return sttRunner.TranscribeWav(
            wavBytes,
            onSuccess: (text) =>
            {
                sttText = text;
                done = true;
            },
            onError: (err) =>
            {
                sttError = err;
                done = true;
            });

        if (!done)
        {
            if (debugLog) Debug.LogWarning("[VoicePTT] STT coroutine completed but callback 미호출");
            yield break;
        }

        if (!string.IsNullOrEmpty(sttError))
        {
            Debug.LogWarning($"[VoicePTT] STT ERROR: {sttError}");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(sttText))
        {
            if (debugLog) Debug.Log("[VoicePTT] STT 결과 비어있음");
            yield break;
        }

        if (debugLog) Debug.Log($"[VoicePTT] STT RESULT: {sttText}");

        if (dispatcher != null)
        {
            dispatcher.EnqueueFromStt(sttText);
        }
    }

    [ContextMenu("Start Mic")]
    public void StartMic()
    {
        if (micReady) return;

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogError("[VoicePTT] 마이크 없음");
            return;
        }

        if (string.IsNullOrWhiteSpace(deviceName))
            deviceName = Microphone.devices[0];

        micClip = Microphone.Start(GetDeviceOrNull(), true, micLoopSeconds, sampleRate);
        if (micClip == null)
        {
            Debug.LogError("[VoicePTT] Microphone.Start 실패");
            return;
        }

        StartCoroutine(WaitMicReady());
    }

    private IEnumerator WaitMicReady()
    {
        float timeout = 2f;
        float t = 0f;

        while (t < timeout)
        {
            int pos = Microphone.GetPosition(GetDeviceOrNull());
            if (pos > 0)
            {
                micReady = true;
                lastMicPos = pos;
                if (debugLog)
                    Debug.Log($"[VoicePTT] Mic ready: {deviceName}, sr={sampleRate}, clipFrames={micClip.samples}");
                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogError("[VoicePTT] 마이크 준비 타임아웃");
    }

    [ContextMenu("Stop Mic")]
    public void StopMic()
    {
        if (!string.IsNullOrWhiteSpace(deviceName) && Microphone.IsRecording(GetDeviceOrNull()))
            Microphone.End(GetDeviceOrNull());

        micReady = false;
        micClip = null;
        isPttPressed = false;
        isFinalizing = false;
    }

    private string GetDeviceOrNull()
    {
        return string.IsNullOrWhiteSpace(deviceName) ? null : deviceName;
    }

    private int MsToFrames(int ms)
    {
        return Mathf.RoundToInt(sampleRate * (ms / 1000f));
    }

    private int WrapFrame(int frame, int totalFrames)
    {
        if (totalFrames <= 0) return 0;
        frame %= totalFrames;
        if (frame < 0) frame += totalFrames;
        return frame;
    }

    private int GetAdvancedFrames(int prevPos, int curPos, int totalFrames)
    {
        if (curPos >= prevPos) return curPos - prevPos;
        return (totalFrames - prevPos) + curPos;
    }

    private float[] ExtractSegmentCircular(AudioClip clip, int startFrame, int endFrame, int channels)
    {
        if (clip == null) return Array.Empty<float>();

        int totalFrames = clip.samples;
        int totalSamples = totalFrames * channels;

        float[] all = new float[totalSamples];
        clip.GetData(all, 0);

        int lengthFrames = (endFrame >= startFrame)
            ? (endFrame - startFrame)
            : ((totalFrames - startFrame) + endFrame);

        if (lengthFrames <= 0)
            return Array.Empty<float>();

        float[] result = new float[lengthFrames * channels];
        int srcFrame = startFrame;
        int dst = 0;

        for (int i = 0; i < lengthFrames; i++)
        {
            int srcBase = srcFrame * channels;
            for (int c = 0; c < channels; c++)
                result[dst++] = all[srcBase + c];

            srcFrame++;
            if (srcFrame >= totalFrames)
                srcFrame = 0;
        }

        return result;
    }
}
