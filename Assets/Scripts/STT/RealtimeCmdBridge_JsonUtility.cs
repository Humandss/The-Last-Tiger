using System;
using UnityEngine;

public class RealtimeCmdBridge_JsonUtility : MonoBehaviour
{
    [SerializeField] private CrewCommandDispatcher dispatcher;
    [Range(0f, 1f)][SerializeField] private float minConfidence = 0.55f;
    [SerializeField] private bool fallbackToSttParser = true;

    public void EnqueueFromRealtimeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        RealtimeCmdWrapper w = null;
        try { w = JsonUtility.FromJson<RealtimeCmdWrapper>(json); }
        catch { }

        if (w == null || w.commands == null || w.commands.Length == 0)
        {
            if (fallbackToSttParser && w != null && !string.IsNullOrWhiteSpace(w.raw_text))
                dispatcher.EnqueueFromStt(w.raw_text);
            return;
        }

        foreach (var c in w.commands)
        {
            if (c == null) continue;

            float conf = c.confidence > 0 ? c.confidence : w.confidence;
            if (conf < minConfidence)
            {
                if (fallbackToSttParser && !string.IsNullOrWhiteSpace(c.raw_text))
                    dispatcher.EnqueueFromStt(c.raw_text);
                continue;
            }

            if (!TryMapRole(c.target_role, out var role)) continue;
            if (!TryMapCmd(c, out var parsed)) continue;

            dispatcher.EnqueueParsed(role, parsed);
        }
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

    private static bool TryMapCmd(RealtimeCmd c, out ParsedCmd parsed)
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

    [Serializable]
    private class RealtimeCmdWrapper
    {
        public string raw_text;
        public float confidence;
        public RealtimeCmd[] commands;
    }

    [Serializable]
    private class RealtimeCmd
    {
        public string target_role;
        public string intent;
        public string intensity;
        public float range_meters;  // ¾øÀ¸¸é -1
        public float confidence;
        public string raw_text;
    }
}
