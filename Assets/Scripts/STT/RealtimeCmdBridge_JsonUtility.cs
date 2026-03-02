using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ✅ 핵심 수정: JsonUtility → 수동 JSON 파싱
///    Unity JsonUtility는 중첩 배열(commands:[...])을 파싱 못함
///    → commands가 항상 null/empty → 항상 STT fallback → CrewParser로 빠져서 오동작
/// </summary>
public class RealtimeCmdBridge_JsonUtility : MonoBehaviour
{
    [SerializeField] private CrewCommandDispatcher dispatcher;
    [Range(0f, 1f)][SerializeField] private float minConfidence = 0.55f;
    [SerializeField] private bool fallbackToSttParser = true;

    public void EnqueueFromRealtimeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        Debug.Log($"[Bridge] 수신 JSON: {json}");

        ParsedWrapper w = null;
        try { w = ParseWrapper(json); }
        catch (Exception e) { Debug.LogWarning($"[Bridge] JSON 파싱 실패: {e.Message}"); }

        if (w == null)
        {
            Debug.LogWarning("[Bridge] w == null, 파싱 실패");
            return;
        }

        Debug.Log($"[Bridge] raw_text={w.raw_text} conf={w.confidence} cmds={w.commands?.Count ?? 0}");

        if (w.commands == null || w.commands.Count == 0)
        {
            Debug.Log("[Bridge] commands 없음");
            if (fallbackToSttParser && !string.IsNullOrWhiteSpace(w.raw_text))
                dispatcher.EnqueueFromStt(w.raw_text);
            return;
        }

        foreach (var c in w.commands)
        {
            if (c == null) continue;

            float conf = c.confidence > 0 ? c.confidence : w.confidence;
            Debug.Log($"[Bridge] cmd: role={c.target_role} intent={c.intent} intensity={c.intensity} conf={conf}");

            if (conf < minConfidence)
            {
                Debug.Log($"[Bridge] confidence 낮아서 스킵: {conf} < {minConfidence}");
                if (fallbackToSttParser && !string.IsNullOrWhiteSpace(c.raw_text))
                    dispatcher.EnqueueFromStt(c.raw_text);
                continue;
            }

            // ✅ RemapRole: driver + gunner intent 조합이면 자동으로 Gunner로 교정
            if (string.IsNullOrWhiteSpace(c.target_role))
            {
                Debug.LogWarning($"[Bridge] 역할 없음, 스킵");
                continue;
            }
            var role = RemapRole(c.target_role, c.intent);
            // driver로 최종 결정되면 dispatcher에서 무시하므로 그냥 통과시켜도 됨
            if (!TryMapCmd(c, out var parsed))
            {
                Debug.LogWarning($"[Bridge] 명령 매핑 실패: {c.intent}");
                continue;
            }

            Debug.Log($"[Bridge] → EnqueueParsed {role} / {parsed}");
            dispatcher.EnqueueParsed(role, parsed);
        }
    }

    // =========================================================
    // ✅ 수동 JSON 파싱 (JsonUtility 대체)
    // =========================================================
    private class ParsedWrapper
    {
        public string raw_text;
        public float confidence;
        public List<ParsedCmd2> commands;
    }

    private class ParsedCmd2
    {
        public string target_role;
        public string intent;
        public string intensity;
        public float range_meters;
        public float confidence;
        public string raw_text;
    }

    private static ParsedWrapper ParseWrapper(string json)
    {
        var w = new ParsedWrapper();
        w.raw_text = ExtractString(json, "raw_text");
        w.confidence = ExtractFloat(json, "confidence");
        w.commands = new List<ParsedCmd2>();

        // commands 배열 추출
        int arrStart = json.IndexOf("\"commands\"");
        if (arrStart < 0) return w;

        arrStart = json.IndexOf('[', arrStart);
        if (arrStart < 0) return w;

        int arrEnd = FindMatchingBracket(json, arrStart, '[', ']');
        if (arrEnd < 0) return w;

        string arrStr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);

        // 배열 안의 각 {} 오브젝트 추출
        int pos = 0;
        while (pos < arrStr.Length)
        {
            int objStart = arrStr.IndexOf('{', pos);
            if (objStart < 0) break;

            int objEnd = FindMatchingBracket(arrStr, objStart, '{', '}');
            if (objEnd < 0) break;

            string obj = arrStr.Substring(objStart, objEnd - objStart + 1);
            var cmd = new ParsedCmd2
            {
                target_role = ExtractString(obj, "target_role"),
                intent = ExtractString(obj, "intent"),
                intensity = ExtractString(obj, "intensity"),
                range_meters = ExtractFloat(obj, "range_meters"),
                confidence = ExtractFloat(obj, "confidence"),
                raw_text = ExtractString(obj, "raw_text"),
            };
            w.commands.Add(cmd);
            pos = objEnd + 1;
        }

        return w;
    }

    /// <summary>JSON에서 "key":"value" 형태의 문자열 값 추출</summary>
    private static string ExtractString(string json, string key)
    {
        var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
    }

    /// <summary>JSON에서 "key":number 형태의 숫자 값 추출</summary>
    private static float ExtractFloat(string json, string key)
    {
        var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*(-?[0-9]+\\.?[0-9]*)");
        if (!m.Success) return 0f;
        return float.TryParse(m.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float v) ? v : 0f;
    }

    /// <summary>괄호 짝 찾기</summary>
    private static int FindMatchingBracket(string s, int start, char open, char close)
    {
        int depth = 0;
        bool inStr = false;
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && inStr) { i++; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == open) depth++;
            if (c == close) { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    // =========================================================
    // 역할/명령 매핑 (기존과 동일)
    // =========================================================
    // ✅ driver로 인식됐어도 gunner intent면 → Gunner로 자동 리매핑
    // STT가 "포수"를 "조종수"로 잘못 들을 때 대비
    private static readonly HashSet<string> GunnerIntents = new HashSet<string>
    {
        "fire", "cease_action", "aim_at", "align_hull", "track_target", "set_range"
    };
    private static readonly HashSet<string> LoaderIntents = new HashSet<string>
    {
        "load_ap", "load_he", "load_default"
    };

    private static CrewRole RemapRole(string roleStr, string intentStr)
    {
        var role = (roleStr ?? "").Trim().ToLowerInvariant();
        var intent = (intentStr ?? "").Trim().ToLowerInvariant();

        // driver로 왔는데 실제 intent가 gunner 계열이면 → Gunner
        if (role == "driver" && GunnerIntents.Contains(intent))
        {
            Debug.Log($"[Bridge] 역할 리매핑: driver → gunner (intent={intent}, STT 오인식 보정)");
            return CrewRole.Gunner;
        }
        // driver로 왔는데 실제 intent가 loader 계열이면 → Loader
        if (role == "driver" && LoaderIntents.Contains(intent))
        {
            Debug.Log($"[Bridge] 역할 리매핑: driver → loader (intent={intent}, STT 오인식 보정)");
            return CrewRole.Loader;
        }

        return role switch
        {
            "gunner" => CrewRole.Gunner,
            "loader" => CrewRole.Loader,
            _ => CrewRole.Driver
        };
    }

    private static bool TryMapRole(string s, out CrewRole role)
    {
        role = CrewRole.Driver;
        if (string.IsNullOrWhiteSpace(s)) return false;
        switch (s.Trim().ToLowerInvariant())
        {
            case "driver": role = CrewRole.Driver; return true;
            case "gunner": role = CrewRole.Gunner; return true;
            case "loader": role = CrewRole.Loader; return true;
            default: return false;
        }
    }

    private static bool TryMapCmd(ParsedCmd2 c, out ParsedCmd parsed)
    {
        parsed = default;
        var intensity = MapIntensity(c.intensity);

        switch ((c.intent ?? "").Trim().ToLowerInvariant())
        {
            case "stop": parsed = new ParsedCmd(Cmd.Stop, Intensity.Normal); return true;
            case "move_forward": parsed = new ParsedCmd(Cmd.MoveForward, intensity); return true;
            case "move_backward": parsed = new ParsedCmd(Cmd.MoveBackward, intensity); return true;
            case "turn_left": parsed = new ParsedCmd(Cmd.TurnLeft, intensity); return true;
            case "turn_right": parsed = new ParsedCmd(Cmd.TurnRight, intensity); return true;
            case "pivot_left": parsed = new ParsedCmd(Cmd.PivotLeft, intensity); return true;
            case "pivot_right": parsed = new ParsedCmd(Cmd.PivotRight, intensity); return true;

            case "fire": parsed = new ParsedCmd(Cmd.Fire, Intensity.Normal); return true;
            case "cease_action": parsed = new ParsedCmd(Cmd.CeaseAction, Intensity.Normal); return true;
            case "aim_at": parsed = new ParsedCmd(Cmd.AimAt, Intensity.Normal); return true;
            case "align_hull": parsed = new ParsedCmd(Cmd.AlignHull, Intensity.Normal); return true;
            case "track_target": parsed = new ParsedCmd(Cmd.TrackTarget, Intensity.Normal); return true;

            case "set_range":
                if (c.range_meters >= 0f)
                {
                    parsed = new ParsedCmd(Cmd.SetRange, Intensity.Normal, c.range_meters);
                    return true;
                }
                return false;

            case "load_ap": parsed = new ParsedCmd(Cmd.LoadAP, Intensity.Normal); return true;
            case "load_he": parsed = new ParsedCmd(Cmd.LoadHE, Intensity.Normal); return true;
            case "load_default": parsed = new ParsedCmd(Cmd.LoadDefault, Intensity.Normal); return true;
        }
        return false;
    }

    private static Intensity MapIntensity(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Intensity.Normal;
        s = s.Trim().ToLowerInvariant();
        if (s == "small" || s == "slow" || s == "low") return Intensity.Small;
        if (s == "large" || s == "fast" || s == "high") return Intensity.Large;
        return Intensity.Normal;
    }
}
