using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class WhisperPttDebugTest : MonoBehaviour
{

    public CrewCommandDispatcher dispatcher;

    [Header("PTT")]
    public KeyCode pushToTalkKey = KeyCode.V;
    public int maxRecordSeconds = 6;

    [Header("Audio")]
    [Tooltip("녹음 샘플레이트(저장은 16kHz로 변환됨)")]
    public int recordSampleRate = 44100;

    [Header("Whisper Options")]
    [Tooltip("스레드 수 (CPU 코어 많으면 8~12 추천)")]
    public int threads = 8;

    private string _micDevice;
    private AudioClip _clip;
    private bool _recording;

    private string WhisperDir => Path.Combine(Application.streamingAssetsPath, "Whisper");
    private string WhisperExe => Path.Combine(WhisperDir, "whisper-cli.exe");
    private string ModelPath => Path.Combine(WhisperDir, "Model", "ggml-small-q5_1.bin");
    private string WavOutPath => Path.Combine(Application.persistentDataPath, "ptt.wav");

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            UnityEngine.Debug.LogError("[WhisperTest] 마이크 장치 없음");
            enabled = false;
            return;
        }

        _micDevice = Microphone.devices[0];
        UnityEngine.Debug.Log($"[WhisperTest] Mic = {_micDevice}");
        UnityEngine.Debug.Log($"[WhisperTest] WhisperExe = {WhisperExe}");
        UnityEngine.Debug.Log($"[WhisperTest] ModelPath  = {ModelPath}");
        UnityEngine.Debug.Log($"[WhisperTest] WavOutPath = {WavOutPath}");
    }

    void Update()
    {
        if (Input.GetKeyDown(pushToTalkKey))
            BeginRecord();

        if (Input.GetKeyUp(pushToTalkKey))
            _ = EndRecordAndTranscribeAsync();
    }

    void BeginRecord()
    {
        if (_recording) return;
        _recording = true;

        UnityEngine.Debug.Log("[WhisperTest] 녹음 시작 (키 누르는 동안 말해)");
        _clip = Microphone.Start(_micDevice, false, maxRecordSeconds, recordSampleRate);
    }

    async Task EndRecordAndTranscribeAsync()
    {
        if (!_recording) return;
        _recording = false;

        int pos = Microphone.GetPosition(_micDevice);
        Microphone.End(_micDevice);

        if (_clip == null || pos <= 0)
        {
            UnityEngine.Debug.LogWarning("[WhisperTest] 녹음 실패/무음");
            return;
        }

        // 1) AudioClip -> float[] trim
        float[] samples = new float[_clip.samples * _clip.channels];
        _clip.GetData(samples, 0);

        int recorded = Mathf.Clamp(pos * _clip.channels, 0, samples.Length);
        float[] trimmed = new float[recorded];
        Array.Copy(samples, trimmed, recorded);

        // 2) WAV 저장 (16k mono PCM16)
        try
        {
            SaveAsWav16kMono(trimmed, _clip.frequency, _clip.channels, WavOutPath);
            UnityEngine.Debug.Log($"[WhisperTest] WAV 저장 완료: {WavOutPath}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[WhisperTest] WAV 저장 실패: {e}");
            return;
        }

        // 3) Whisper 실행
        string result = await RunWhisperAsync(WavOutPath);

        // 4) 로그 출력
        UnityEngine.Debug.Log($"[WhisperTest] 인식 결과: {result}");

        dispatcher?.EnqueueFromStt(result);
    }

    async Task<string> RunWhisperAsync(string wavPath)
    {
        if (!File.Exists(WhisperExe)) return $"[에러] whisper-cli.exe 없음: {WhisperExe}";
        if (!File.Exists(ModelPath)) return $"[에러] 모델 없음: {ModelPath}";
        if (!File.Exists(wavPath)) return $"[에러] wav 없음: {wavPath}";

        // DLL 로딩을 위해 WorkingDirectory가 중요
        var psi = new ProcessStartInfo
        {
            FileName = WhisperExe,
            WorkingDirectory = WhisperDir,
            Arguments = $"-m \"{ModelPath}\" -f \"{wavPath}\" -l ko --no-timestamps -t {threads}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var p = new Process { StartInfo = psi };
            p.Start();

            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            await Task.Run(() => p.WaitForExit());

            if (p.ExitCode != 0)
                return $"[STT 실패] exit={p.ExitCode}\n{stderr}\n{stdout}";

            // whisper는 로그가 섞일 수 있어서 "마지막 의미있는 줄"만 뽑아줌
            string cleaned = ExtractLastTextLine(stdout);
            if (!string.IsNullOrWhiteSpace(cleaned)) return cleaned.Trim();

            // stdout이 비어있으면 stderr에 출력하는 빌드도 있어서 fallback
            cleaned = ExtractLastTextLine(stderr);
            return string.IsNullOrWhiteSpace(cleaned) ? stdout.Trim() : cleaned.Trim();
        }
        catch (Exception e)
        {
            return $"[예외] whisper 실행 실패: {e.Message}";
        }
    }

    static string ExtractLastTextLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var lines = s.Replace("\r\n", "\n").Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // 로그 라인들 대충 걸러내기
            if (line.StartsWith("whisper_") || line.StartsWith("main:") || line.StartsWith("system_info:"))
                continue;
            if (line.Contains("time =") || line.Contains("load time") || line.Contains("total time"))
                continue;

            return line;
        }
        return "";
    }

    // ---- WAV 저장 유틸: 16kHz mono PCM16 ----
    static void SaveAsWav16kMono(float[] interleaved, int srcHz, int channels, string outPath)
    {
        float[] mono = (channels == 1) ? interleaved : DownmixToMono(interleaved, channels);
        const int dstHz = 16000;
        float[] resampled = ResampleLinear(mono, srcHz, dstHz);

        short[] pcm16 = new short[resampled.Length];
        for (int i = 0; i < resampled.Length; i++)
        {
            float v = Mathf.Clamp(resampled[i], -1f, 1f);
            pcm16[i] = (short)Mathf.RoundToInt(v * 32767f);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        int byteRate = dstHz * 1 * 2;
        int dataSize = pcm16.Length * 2;

        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);   // PCM
        bw.Write((short)1);   // mono
        bw.Write(dstHz);
        bw.Write(byteRate);
        bw.Write((short)2);   // block align
        bw.Write((short)16);  // bits

        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);

        for (int i = 0; i < pcm16.Length; i++)
            bw.Write(pcm16[i]);
    }

    static float[] DownmixToMono(float[] interleaved, int channels)
    {
        int frames = interleaved.Length / channels;
        float[] mono = new float[frames];

        for (int f = 0; f < frames; f++)
        {
            float sum = 0f;
            int baseIdx = f * channels;
            for (int c = 0; c < channels; c++)
                sum += interleaved[baseIdx + c];
            mono[f] = sum / channels;
        }
        return mono;
    }

    static float[] ResampleLinear(float[] src, int srcHz, int dstHz)
    {
        if (srcHz == dstHz) return src;

        double ratio = (double)dstHz / srcHz;
        int dstLen = (int)Math.Floor(src.Length * ratio);
        float[] dst = new float[dstLen];

        for (int i = 0; i < dstLen; i++)
        {
            double srcPos = i / ratio;
            int i0 = (int)Math.Floor(srcPos);
            int i1 = Math.Min(i0 + 1, src.Length - 1);
            float t = (float)(srcPos - i0);
            dst[i] = Mathf.Lerp(src[i0], src[i1], t);
        }
        return dst;
    }
}
