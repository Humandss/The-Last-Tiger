using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class RealtimeCmdBridge : MonoBehaviour
{
    [SerializeField] private CrewCommandDispatcher dispatcher;

    [Header("Safety")]
    [Range(0f, 1f)]
    [SerializeField] private float minConfidence = 0.55f;

    [SerializeField] private bool fallbackToSttParser = true;

    public void EnqueueFromRealtimeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        RealtimeCmdWrapper w = null;
        try
        {
            w = JsonUtility.FromJson<RealtimeCmdWrapper>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RealtimeCmdBridge] Json parse exception: {e.Message}\n{json}");
        }

        if (w == null)
        {
            if (fallbackToSttParser) dispatcher.EnqueueFromStt(json);
            return;
        }

        // wrapper raw fallback
        if ((w.commands == null || w.commands.Length == 0))
        {
            if (fallbackToSttParser && !string.IsNullOrWhiteSpace(w.raw_text))
                dispatcher.EnqueueFromStt(w.raw_text);
            return;
        }

        foreach (var c in w.commands)
        {
            if (c == null) continue;

            float conf = (c.confidence > 0f) ? c.confidence : w.confidence;
            if (conf < minConfidence)
            {
                if (fallbackToSttParser && !string.IsNullOrWhiteSpace(c.raw_text))
                    dispatcher.EnqueueFromStt(c.raw_text);
                continue;
            }

            if (!TryMapRole(c.target_role, out var role))
            {
                if (fallbackToSttParser && !string.IsNullOrWhiteSpace(c.raw_text))
                    dispatcher.EnqueueFromStt(c.raw_text);
                continue;
            }

            if (!TryMapCmd(c, out var parsed))
            {
                if (fallbackToSttParser && !string.IsNullOrWhiteSpace(c.raw_text))
                    dispatcher.EnqueueFromStt(c.raw_text);
                continue;
            }

            dispatcher.EnqueueParsed(role, parsed);
        }
    }

    private static bool TryMapRole(string roleStr, out CrewRole role)
    {
        role = CrewRole.Driver;
        if (string.IsNullOrWhiteSpace(roleStr)) return false;

        switch (roleStr.Trim().ToLowerInvariant())
        {
            case "driver":
            case "조종수":
            case "운전수":
                role = CrewRole.Driver; return true;

            case "gunner":
            case "포수":
                role = CrewRole.Gunner; return true;

            case "loader":
            case "장전수":
                role = CrewRole.Loader; return true;
        }
        return false;
    }

    private static bool TryMapCmd(RealtimeCmd c, out ParsedCmd parsed)
    {
        parsed = default;

        var intensity = MapIntensity(c.intensity);
        float range = c.range_meters;

        if (string.IsNullOrWhiteSpace(c.intent)) return false;

        switch (c.intent.Trim().ToLowerInvariant())
        {
            // Driver
            case "stop":
                parsed = new ParsedCmd(Cmd.Stop, Intensity.Normal); return true;

            case "move_forward":
                parsed = new ParsedCmd(Cmd.MoveForward, intensity); return true;

            case "move_backward":
                parsed = new ParsedCmd(Cmd.MoveBackward, intensity); return true;

            case "turn_left":
                parsed = new ParsedCmd(Cmd.TurnLeft, intensity); return true;

            case "turn_right":
                parsed = new ParsedCmd(Cmd.TurnRight, intensity); return true;

            case "pivot_left":
                parsed = new ParsedCmd(Cmd.PivotLeft, intensity); return true;

            case "pivot_right":
                parsed = new ParsedCmd(Cmd.PivotRight, intensity); return true;

            // Gunner
            case "fire":
                parsed = new ParsedCmd(Cmd.Fire, Intensity.Normal); return true;

            case "cease":
            case "cease_action":
            case "cancel":
                parsed = new ParsedCmd(Cmd.CeaseAction, Intensity.Normal); return true;

            case "aim":
            case "aim_at":
                parsed = new ParsedCmd(Cmd.AimAt, Intensity.Normal); return true;

            case "align":
            case "align_hull":
            case "reset":
                parsed = new ParsedCmd(Cmd.AlignHull, Intensity.Normal); return true;

            case "set_range":
                if (range >= 0f)
                {
                    parsed = new ParsedCmd(Cmd.SetRange, Intensity.Normal, range);
                    return true;
                }
                return false;

            case "track":
            case "track_target":
            case "lock_on":
                parsed = new ParsedCmd(Cmd.TrackTarget, Intensity.Normal); return true;

            // Loader
            case "load_ap":
                parsed = new ParsedCmd(Cmd.LoadAP, Intensity.Normal); return true;

            case "load_he":
                parsed = new ParsedCmd(Cmd.LoadHE, Intensity.Normal); return true;

            case "load_default":
            case "reload":
                parsed = new ParsedCmd(Cmd.LoadDefault, Intensity.Normal); return true;
        }

        return false;
    }

    private static Intensity MapIntensity(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Intensity.Normal;

        switch (s.Trim().ToLowerInvariant())
        {
            case "small":
            case "slow":
            case "low":
                return Intensity.Small;

            case "large":
            case "fast":
            case "high":
                return Intensity.Large;

            default:
                return Intensity.Normal;
        }
    }
}

[Serializable]
public class RealtimeCmdWrapper
{
    public string raw_text;
    public float confidence;
    public RealtimeCmd[] commands;
}

[Serializable]
public class RealtimeCmd
{
    public string target_role;
    public string intent;
    public string intensity;
    public float range_meters; // 없으면 -1로 보내기
    public float confidence;
    public string raw_text;
}
