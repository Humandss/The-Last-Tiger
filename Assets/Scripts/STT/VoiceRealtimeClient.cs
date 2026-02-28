using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;
using WebSocketSharp;

/// <summary>
/// V키를 누르는 동안 마이크를 캡처해서 PCM16(base64) 프레임을 서버로 전송.
/// V키를 떼면 commit 전송.
/// 서버가 보내는 cmdjson을 RealtimeCmdBridge_JsonUtility로 전달.
/// </summary>
public class VoiceRealtimeClient : MonoBehaviour
{
    [Header("Server")]
    [SerializeField] private string url = "ws://localhost:8787/unity";

    [Header("Refs")]
    [SerializeField] private RealtimeCmdBridge_JsonUtility bridge;

    [Header("PTT")]
    [SerializeField] private KeyCode pttKey = KeyCode.V;

    [Header("Mic")]
    [SerializeField] private int sampleRate = 16000;     // 서버/Realtime에 맞춰 16k 권장
    [SerializeField] private int frameMs = 50;           // 20~100ms 권장(50ms 무난)
    [SerializeField] private bool logDebug = false;

    private WebSocket ws;

    private AudioClip micClip;
    private string micDevice;
    private bool pttHeld;

    private int lastSamplePos;      // AudioClip 내 마지막으로 읽은 sample 위치
    private int frameSamples;       // frameMs에 해당하는 샘플 수
    private float[] floatBuf;       // 마이크 float 샘플 버퍼
    private byte[] pcm16Buf;        // PCM16 바이트 버퍼 (2 * frameSamples)

    int sentFrames;
    float nextLog;

    private float[] ring;       // 마이크 전체(1초) 버퍼
    private int ringSize;       // = micClip.samples

    // WS 콜백은 메인스레드 아닐 수 있어서 큐로 처리
    private readonly Queue<Action> mainThreadQ = new();

    [Serializable]
    private class ServerMsg
    {
        public string type;
        public string json;
    }

    [Serializable]
    private class ClientAudioMsg
    {
        public string type; // "audio"
        public string b64;
    }

    [Serializable]
    private class ClientCommitMsg
    {
        public string type; // "commit"
    }

    void Start()
    {
        // 1) WS 연결
        ws = new WebSocket(url);

        ws.OnOpen += (_, __) => EnqueueMain(() => Debug.Log("[VoiceWS] Connected"));
        ws.OnClose += (_, e) => EnqueueMain(() => Debug.Log($"[VoiceWS] Closed: {e.Reason}"));
        ws.OnError += (_, e) => EnqueueMain(() => Debug.LogError($"[VoiceWS] Error: {e.Message}"));

        ws.OnMessage += (_, e) =>
        {
            // 서버 → Unity: {type:"cmdjson", json:"{...}"}
            try
            {
                var msg = JsonUtility.FromJson<ServerMsg>(e.Data);
                if (msg != null && msg.type == "cmdjson" && !string.IsNullOrWhiteSpace(msg.json))
                {
                    EnqueueMain(() =>
                    {
                        if (logDebug) Debug.Log($"[VoiceWS][cmdjson] {msg.json}");
                        bridge.EnqueueFromRealtimeJson(msg.json);
                    });
                }
            }
            catch (Exception ex)
            {
                EnqueueMain(() => Debug.LogWarning($"[VoiceWS] Parse fail: {ex.Message}\n{e.Data}"));
            }
        };

        ws.ConnectAsync();

        // 2) 마이크 디바이스 선택 (기본 0번)
        if (Microphone.devices != null && Microphone.devices.Length > 0)
            micDevice = Microphone.devices[0];
        else
            Debug.LogWarning("[Voice] No microphone devices found.");
    }

    void Update()
    {
        // WS 이벤트 메인스레드 처리
        while (mainThreadQ.Count > 0)
        {
            var a = mainThreadQ.Dequeue();
            a?.Invoke();
        }

        // PTT 입력
        if (Input.GetKeyDown(pttKey))
            BeginPTT();

        if (Input.GetKeyUp(pttKey))
            EndPTT();

        // PTT 유지 중이면 오디오 프레임 전송
        if (pttHeld)
            PumpMicFrames();
    }

    void OnDestroy()
    {
        if (Microphone.IsRecording(micDevice))
            Microphone.End(micDevice);

        if (ws != null)
        {
            try { ws.Close(); } catch { }
            ws = null;
        }
    }

    private void BeginPTT()
    {
        if (pttHeld) return;
        if (string.IsNullOrEmpty(micDevice)) return;

        pttHeld = true;

        micClip = Microphone.Start(micDevice, true, 1, 48000); // 일단 48k로 고정(안 되면 Unity가 맞춰줌)
        lastSamplePos = 0;

        ringSize = micClip.samples;
        ring = new float[ringSize];

        Debug.Log($"[Voice] micClip freq={micClip.frequency} samples={micClip.samples} channels={micClip.channels}");

        int sr = micClip.frequency;
        frameSamples = Mathf.CeilToInt(sr * (frameMs / 1000f));
        floatBuf = new float[frameSamples];
        pcm16Buf = new byte[frameSamples * 2];

        if (logDebug) Debug.Log("[Voice] PTT Begin");
    }

    private void EndPTT()
    {
        if (!pttHeld) return;
        pttHeld = false;

        StartCoroutine(EndPttRoutine());
    }

    private System.Collections.IEnumerator EndPttRoutine()
    {
        // 1) 조금 더 수집
        float t = 0.25f;
        float end = Time.time + t;

        while (Time.time < end)
        {
            PumpMicFrames();
            yield return null;
        }

        // 2) 마지막 한번 더
        PumpMicFrames();

        // 3) 마이크 정지
        if (Microphone.IsRecording(micDevice))
            Microphone.End(micDevice);

        micClip = null;

        // 4) commit 전송
        SendCommit();
    }

    private void PumpMicFrames()
    {
        if (micClip == null) return;
        if (ws == null || ws.ReadyState != WebSocketState.Open) return;

        int curPos = Microphone.GetPosition(micDevice);
        if (curPos < 0) return;

        // 1) 마이크 1초 ring 전체를 읽어옴 (안전)
        micClip.GetData(ring, 0);

        // 2) available 계산
        int available = (curPos >= lastSamplePos)
            ? (curPos - lastSamplePos)
            : (ringSize - lastSamplePos + curPos);

        if (logDebug)
           // Debug.Log($"[Voice] cur={curPos} last={lastSamplePos} avail={available} frame={frameSamples} freq={micClip.frequency}");

        while (available >= frameSamples)
        {
                float rms = 0f;
                for (int i = 0; i < floatBuf.Length; i++) rms += floatBuf[i] * floatBuf[i];
                rms = Mathf.Sqrt(rms / floatBuf.Length);

                if (logDebug && Time.frameCount % 30 == 0)
                    Debug.Log($"[Voice] rms={rms:0.0000}");

                // 3) ring에서 frameSamples만큼 뽑아 floatBuf에 채움
                ReadFromRing(ring, ringSize, lastSamplePos, floatBuf);

            lastSamplePos = (lastSamplePos + frameSamples) % ringSize;
            available -= frameSamples;

            FloatToPcm16(floatBuf, pcm16Buf);
            SendAudio(Convert.ToBase64String(pcm16Buf));
        }
    }

    private static void ReadFromRing(float[] ring, int ringSize, int start, float[] dst)
    {
        int need = dst.Length;
        int remain = ringSize - start;

        if (remain >= need)
        {
            Array.Copy(ring, start, dst, 0, need);
        }
        else
        {
            Array.Copy(ring, start, dst, 0, remain);
            Array.Copy(ring, 0, dst, remain, need - remain);
        }
    }


    private static void FloatToPcm16(float[] src, byte[] dst)
    {
        // little-endian PCM16
        for (int i = 0; i < src.Length; i++)
        {
            float v = Mathf.Clamp(src[i], -1f, 1f);
            short s = (short)Mathf.RoundToInt(v * 32767f);

            int bi = i * 2;
            dst[bi] = (byte)(s & 0xFF);
            dst[bi + 1] = (byte)((s >> 8) & 0xFF);
        }
    }

    private void SendAudio(string b64)
    {
        var msg = new ClientAudioMsg { type = "audio", b64 = b64 };
        ws.SendAsync(JsonUtility.ToJson(msg), null);

        sentFrames++;

        if (logDebug && Time.time >= nextLog)
        {
            Debug.Log($"[Voice] sentFrames={sentFrames}/sec ws={ws.ReadyState}");
            sentFrames = 0;
            nextLog = Time.time + 1f;
        }
    }

    private void SendCommit()
    {
        if (ws == null || ws.ReadyState != WebSocketState.Open) return;
        var msg = new ClientCommitMsg { type = "commit" };
        ws.SendAsync(JsonUtility.ToJson(msg), null);
        Debug.Log("[Voice] commit sent");
    }

    private void EnqueueMain(Action a)
    {
        lock (mainThreadQ) mainThreadQ.Enqueue(a);
    }
}