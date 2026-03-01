using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;

/// <summary>
/// 개선사항:
/// ✅ Whisper prompt를 실제 발화 문장 형태로 변경 (포수/조종수 혼동 방지)
/// ✅ 무음 게이트 holdoff 추가 (단어 사이 짧은 무음에 잘림 방지)
/// ✅ 마이크 워밍업 100ms로 단축 (앞부분 잘림과 노이즈 트레이드오프 개선)
/// ✅ tail 500ms로 증가 (한국어 어말 폐쇄음 잘림 방지 — "탄", "진", "수")
/// ✅ Pre-emphasis 기본 OFF (Whisper는 원본 오디오 선호)
/// ✅ AGC target RMS 낮춤 (과증폭으로 인한 클리핑 방지)
/// ✅ 샘플레이트 확인 로그 추가
/// </summary>
public class VoiceRealtimeClient : MonoBehaviour
{
    [Header("Server")]
    [SerializeField] private string url = "ws://localhost:8787/unity";

    [Header("Refs")]
    [SerializeField] private RealtimeCmdBridge_JsonUtility bridge;

    [Header("PTT")]
    [SerializeField] private KeyCode pttKey = KeyCode.V;

    [Header("Audio Processing")]
    // ✅ Pre-emphasis 기본 OFF — Whisper는 원본 오디오에서 더 정확함
    //    ON 시 자음 고주파 강조 효과가 있지만 오히려 왜곡이 생길 수 있음
    [SerializeField] private bool usePreEmphasis = false;
    [SerializeField] private bool useAGC = true;
    [SerializeField] private bool useSilenceGate = true;
    // ✅ AGC target RMS 낮춤: 0.15 → 0.12 (과증폭/클리핑 방지)
    [SerializeField] private float agcTargetRms = 0.12f;
    [SerializeField] private float agcMaxGain = 8f;
    [SerializeField] private float agcMinGain = 0.5f;
    [SerializeField] private float preEmphAlpha = 0.97f;
    // ✅ 무음 임계값 낮춤: 0.005 → 0.002 (조용히 말해도 잘리지 않게)
    [SerializeField] private float silenceThreshold = 0.002f;
    // ✅ tail 늘림: 0.30 → 0.50 (한국어 어말 자음 "탄", "진", "수" 잘림 방지)
    [SerializeField] private float tailSeconds = 0.50f;
    // ✅ holdoff 프레임: 단어 사이 짧은 무음(숨 고르기 등)에 게이트가 끊기지 않도록
    //    8프레임 × 20ms = 160ms 동안 조용해도 전송 유지
    [SerializeField] private int silenceHoldoffFrames = 8;

    [Header("PTT Timing")]
    // ✅ 워밍업 단축: 0.15 → 0.10 (앞 음절 잘림 vs 노이즈 트레이드오프)
    //    필요시 Inspector에서 조절
    [SerializeField] private float micWarmupSeconds = 0.10f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private const int TARGET_SR = 24000;
    private const int FALLBACK_SR = 48000;
    private const int FRAME_MS = 20;

    private WebSocket ws;
    private AudioClip micClip;
    private string micDevice;
    private bool pttHeld;
    private bool micReady;

    private int captureSR;
    private int captureFrameSamples;
    private int targetFrameSamples;
    private float[] floatBuf;
    private float[] resampledBuf;
    private byte[] pcm16Buf;
    private float[] ring;
    private int ringSize;
    private int lastSamplePos;

    private float _agcGain = 1f;
    private int sentFrames = 0;
    private int skippedFrames = 0;
    private float nextLog = 0f;

    // ✅ 무음 holdoff 카운터
    private int _silentFrameCount = 0;

    private readonly Queue<Action> mainThreadQ = new Queue<Action>();

    [Serializable] private class ServerMsg { public string type; public string json; }
    [Serializable] private class ClientAudioMsg { public string type; public string b64; }
    [Serializable] private class ClientCommitMsg { public string type; }

    void Start()
    {
        ws = new WebSocket(url);
        ws.OnOpen += (_, __) => EnqueueMain(() => Debug.Log("[VoiceWS] Connected"));
        ws.OnClose += (_, e) => EnqueueMain(() => Debug.Log($"[VoiceWS] Closed: {e.Reason}"));
        ws.OnError += (_, e) => EnqueueMain(() => Debug.LogError($"[VoiceWS] Error: {e.Message}"));
        ws.OnMessage += (_, e) =>
        {
            try
            {
                var msg = JsonUtility.FromJson<ServerMsg>(e.Data);
                if (msg?.type == "cmdjson" && !string.IsNullOrWhiteSpace(msg.json))
                    EnqueueMain(() =>
                    {
                        if (logDebug) Debug.Log($"[VoiceWS] cmdjson: {msg.json}");
                        bridge.EnqueueFromRealtimeJson(msg.json);
                    });
            }
            catch (Exception ex)
            {
                EnqueueMain(() => Debug.LogWarning($"[VoiceWS] Parse fail: {ex.Message}"));
            }
        };

        ws.ConnectAsync();

        micDevice = (Microphone.devices?.Length > 0) ? Microphone.devices[0] : null;
        if (micDevice == null)
            Debug.LogWarning("[Voice] No microphone found.");
        else
            Debug.Log($"[Voice] Using mic: {micDevice}");
    }

    void Update()
    {
        lock (mainThreadQ)
            while (mainThreadQ.Count > 0)
                mainThreadQ.Dequeue()?.Invoke();

        if (Input.GetKeyDown(pttKey)) BeginPTT();
        if (Input.GetKeyUp(pttKey)) EndPTT();

        if (pttHeld && micReady) PumpMicFrames();
    }

    void OnDestroy()
    {
        if (Microphone.IsRecording(micDevice)) Microphone.End(micDevice);
        try { ws?.Close(); } catch { }
        ws = null;
    }

    private void BeginPTT()
    {
        if (pttHeld || micDevice == null) return;
        pttHeld = true;
        micReady = false;
        _agcGain = 1f;
        _silentFrameCount = 0;
        sentFrames = 0;
        skippedFrames = 0;

        // 24kHz 직접 캡처 시도, 불가 시 48kHz fallback
        micClip = Microphone.Start(micDevice, true, 1, TARGET_SR);
        captureSR = micClip.frequency;

        if (captureSR != TARGET_SR)
        {
            Microphone.End(micDevice);
            micClip = Microphone.Start(micDevice, true, 1, FALLBACK_SR);
            captureSR = micClip.frequency;
        }

        ringSize = micClip.samples;
        ring = new float[ringSize];

        captureFrameSamples = Mathf.CeilToInt(captureSR * (FRAME_MS / 1000f));
        targetFrameSamples = Mathf.CeilToInt(TARGET_SR * (FRAME_MS / 1000f));

        floatBuf = new float[captureFrameSamples];
        resampledBuf = new float[targetFrameSamples];
        pcm16Buf = new byte[targetFrameSamples * 2];

        // ✅ 샘플레이트 확인 로그 — 리샘플링 여부 즉시 확인 가능
        Debug.Log($"[Voice] PTT Begin | captureSR={captureSR}Hz | targetSR={TARGET_SR}Hz | " +
                  $"resampling={captureSR != TARGET_SR} | warmup={micWarmupSeconds * 1000:0}ms");

        StartCoroutine(MicWarmupRoutine());
    }

    /// <summary>
    /// 마이크 워밍업: 일정 시간 대기 후 현재 마이크 위치를 기준점으로 설정.
    /// 워밍업 중 들어온 노이즈/무음 버퍼를 스킵하고 이 시점부터 전송 시작.
    /// </summary>
    private IEnumerator MicWarmupRoutine()
    {
        yield return new WaitForSeconds(micWarmupSeconds);

        if (!pttHeld) yield break;

        lastSamplePos = Microphone.GetPosition(micDevice);
        micReady = true;
        _silentFrameCount = 0;

        Debug.Log($"[Voice] Mic ready at pos={lastSamplePos}");
    }

    private void EndPTT()
    {
        if (!pttHeld) return;
        pttHeld = false;
        micReady = false;
        StartCoroutine(EndPttRoutine());
    }

    private IEnumerator EndPttRoutine()
    {
        // tail: 말 끝 잘림 방지 (한국어 어말 폐쇄음 대비 500ms)
        micReady = true;
        float end = Time.time + tailSeconds;
        while (Time.time < end) { PumpMicFrames(); yield return null; }
        PumpMicFrames();

        if (Microphone.IsRecording(micDevice)) Microphone.End(micDevice);
        micClip = null;
        micReady = false;

        Debug.Log($"[Voice] PTT End | sent={sentFrames} skipped={skippedFrames}");
        SendCommit();
    }

    private void PumpMicFrames()
    {
        if (micClip == null) return;
        if (ws?.ReadyState != WebSocketState.Open) return;

        int curPos = Microphone.GetPosition(micDevice);
        if (curPos < 0) return;

        micClip.GetData(ring, 0);

        int available = (curPos >= lastSamplePos)
            ? curPos - lastSamplePos
            : ringSize - lastSamplePos + curPos;

        while (available >= captureFrameSamples)
        {
            ReadFromRing(ring, ringSize, lastSamplePos, floatBuf);
            lastSamplePos = (lastSamplePos + captureFrameSamples) % ringSize;
            available -= captureFrameSamples;

            float rms = CalcRms(floatBuf);

            // ✅ 개선된 무음 게이트: holdoff 적용
            //    단순 RMS 임계값 → 연속 N프레임 조용할 때만 스킵
            //    단어 사이 숨 고르기(~100ms) 정도는 잘리지 않음
            if (useSilenceGate && rms < silenceThreshold)
            {
                _silentFrameCount++;
                if (_silentFrameCount >= silenceHoldoffFrames)
                {
                    skippedFrames++;
                    continue;  // holdoff 이상 연속 무음이면 스킵
                }
                // holdoff 이내는 무음이어도 그냥 전송 (프레임 연속성 유지)
            }
            else
            {
                _silentFrameCount = 0;  // 소리 있으면 카운터 리셋
            }

            if (usePreEmphasis) ApplyPreEmphasis(floatBuf, preEmphAlpha);
            if (useAGC) ApplyAGC(floatBuf, rms);

            float[] sendBuf;
            if (captureSR == TARGET_SR)
                sendBuf = floatBuf;
            else
            {
                ResampleLanczos(floatBuf, resampledBuf);
                sendBuf = resampledBuf;
            }

            FloatToPcm16(sendBuf, pcm16Buf);
            SendAudio(Convert.ToBase64String(pcm16Buf));
            sentFrames++;

            if (logDebug && Time.time >= nextLog)
            {
                Debug.Log($"[Voice] sent={sentFrames} skip={skippedFrames} " +
                          $"rms={rms:0.0000} gain={_agcGain:0.00} silentFrames={_silentFrameCount}");
                nextLog = Time.time + 1f;
            }
        }
    }

    // =========================================================
    // DSP
    // =========================================================

    private static float CalcRms(float[] buf)
    {
        float sum = 0f;
        foreach (var s in buf) sum += s * s;
        return Mathf.Sqrt(sum / buf.Length);
    }

    private static void ApplyPreEmphasis(float[] buf, float alpha)
    {
        for (int i = buf.Length - 1; i > 0; i--)
            buf[i] -= alpha * buf[i - 1];
    }

    private void ApplyAGC(float[] buf, float currentRms)
    {
        if (currentRms < 0.0001f) return;
        float targetGain = Mathf.Clamp(agcTargetRms / currentRms, agcMinGain, agcMaxGain);
        _agcGain = Mathf.Lerp(_agcGain, targetGain, 0.1f);
        for (int i = 0; i < buf.Length; i++)
            buf[i] = Mathf.Clamp(buf[i] * _agcGain, -1f, 1f);
    }

    private static void ResampleLanczos(float[] src, float[] dst, int a = 3)
    {
        int srcLen = src.Length;
        int dstLen = dst.Length;
        float ratio = (float)srcLen / dstLen;

        for (int i = 0; i < dstLen; i++)
        {
            float pos = i * ratio;
            int center = (int)Mathf.Floor(pos);
            float sum = 0f, weight = 0f;

            for (int j = center - a + 1; j <= center + a; j++)
            {
                if (j < 0 || j >= srcLen) continue;
                float w = LanczosKernel(pos - j, a);
                sum += src[j] * w;
                weight += w;
            }
            dst[i] = weight > 0f ? Mathf.Clamp(sum / weight, -1f, 1f) : 0f;
        }
    }

    private static float LanczosKernel(float x, int a)
    {
        if (Mathf.Abs(x) < 1e-6f) return 1f;
        if (Mathf.Abs(x) >= a) return 0f;
        float px = Mathf.PI * x;
        return (a * Mathf.Sin(px) * Mathf.Sin(px / a)) / (px * px);
    }

    // =========================================================
    // 유틸
    // =========================================================

    private static void ReadFromRing(float[] ring, int ringSize, int start, float[] dst)
    {
        int need = dst.Length, remain = ringSize - start;
        if (remain >= need)
            Array.Copy(ring, start, dst, 0, need);
        else
        {
            Array.Copy(ring, start, dst, 0, remain);
            Array.Copy(ring, 0, dst, remain, need - remain);
        }
    }

    private static void FloatToPcm16(float[] src, byte[] dst)
    {
        for (int i = 0; i < src.Length; i++)
        {
            short s = (short)Mathf.RoundToInt(Mathf.Clamp(src[i], -1f, 1f) * 32767f);
            int bi = i * 2;
            dst[bi] = (byte)(s & 0xFF);
            dst[bi + 1] = (byte)((s >> 8) & 0xFF);
        }
    }

    private void SendAudio(string b64)
        => ws.SendAsync(JsonUtility.ToJson(new ClientAudioMsg { type = "audio", b64 = b64 }), null);

    private void SendCommit()
    {
        if (ws?.ReadyState != WebSocketState.Open) return;
        ws.SendAsync(JsonUtility.ToJson(new ClientCommitMsg { type = "commit" }), null);
        Debug.Log("[Voice] commit sent");
    }

    private void EnqueueMain(Action a) { lock (mainThreadQ) mainThreadQ.Enqueue(a); }
}