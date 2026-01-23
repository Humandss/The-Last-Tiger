using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public enum CrewRole { Driver, Gunner, Loader }
public enum Cmd { 
    MoveForward, MoveBackward, Stop, TurnLeft, TurnRight, PivotLeft, PivotRight,
    Fire, 
    LoadAP, LoadHE 
}
public enum Intensity
{
    Small,   // 조금
    Normal,  // 보통(기본)
    Large    // 크게
}
// cmd 구조체
public readonly struct ParsedCmd
{
    public readonly Cmd cmd;
    public readonly Intensity intensity;

    public ParsedCmd(Cmd cmd, Intensity intensity)
    {
        this.cmd = cmd;
        this.intensity = intensity;
    }

    public override string ToString() => intensity == Intensity.Normal ? cmd.ToString() : $"{cmd}({intensity})";
}

public static class CrewParser
{
    static readonly (CrewRole role, string[] keys)[] RoleKeys =
    {
        (CrewRole.Driver, new[] { "조", "조종수", "조종", "운전수", "드라이버" }),
        (CrewRole.Gunner, new[] { "포", "포수", "보수", "포스", "포주", "거너" }),
        (CrewRole.Loader, new[] { "장", "장전수", "장전", "로더" }),
    };

    static Intensity ParseIntensity(string seg)
    {
        if (ContainsAny(seg, LargeK)) return Intensity.Large;
        if (ContainsAny(seg, SmallK)) return Intensity.Small;
        return Intensity.Normal;
    }


    //드라이버 CMD
    static readonly string[] Fwd = { "전진", "앞으로", "전방"};
    static readonly string[] Back = { "후진", "뒤로", "후방", "백" };
    static readonly string[] Stop = { "정지", "멈춰", "스톱", "서", "그만" };
    static readonly string[] TurnL = { "좌회전", "왼쪽", "좌로" };
    static readonly string[] TurnR = { "우회전", "오른쪽", "우로" };
    static readonly string[] PivotL = { "제자리 좌회전", "제자리 왼쪽", "피벗 좌" };
    static readonly string[] PivotR = { "제자리 우회전", "제자리 오른쪽", "피벗 우" };
    // 강도
    static readonly string[] SmallK = { "조금", "살짝", "약하게" , "천천히" };
    static readonly string[] LargeK = { "크게", "많이", "강하게", "빠르게","빨리"};
    // 기본값 Normal
    static readonly string[] NormalK = { "보통", "적당히", "그대로", "유지" };


    static readonly string[] Fire = { "발사", "사격", "격발", "쏴", "쏴라" };

    static readonly string[] AP = { "철갑", "ap", "철갑탄" };
    static readonly string[] HE = { "고폭", "he", "고폭탄" };

    public static Dictionary<CrewRole, List<ParsedCmd>> Parse(string stt)
    {
        stt = Normalize(stt);
        var output = new Dictionary<CrewRole, List<ParsedCmd>>();
        if (string.IsNullOrWhiteSpace(stt)) return output;

        var marks = FindRoleMarks(stt);

        // 역할이 언급되지 않은 짧은 전차장 명령 처리
        if (marks.Count == 0)
        {
            var inferred = InferRole(stt);
            output[inferred] = ParseCmds(inferred, stt); // List<ParsedCmd>
            return output;
        }

        marks = marks.OrderBy(m => m.idx).ToList();

        for (int i = 0; i < marks.Count; i++)
        {
            var cur = marks[i];
            int segStart = cur.idx + cur.len;
            int segEnd = (i + 1 < marks.Count) ? marks[i + 1].idx : stt.Length;

            string seg = stt.Substring(segStart, Math.Max(0, segEnd - segStart)).Trim();
            if (seg.Length == 0) continue;

            var cmds = ParseCmds(cur.role, seg); // List<ParsedCmd>
            if (cmds.Count == 0) continue;

            if (!output.TryGetValue(cur.role, out var list))
                output[cur.role] = list = new List<ParsedCmd>();

            list.AddRange(cmds);
        }

        return output;
    }

    static string Normalize(string s)
    {
        s = s.Trim();
        s = s.Replace("!", " ").Replace(".", " ").Replace(",", " ").Replace("?", " ");
        s = Regex.Replace(s, @"\s+", " ");
        return s;
    }

    static List<(int idx, int len, CrewRole role)> FindRoleMarks(string s)
    {
        var marks = new List<(int idx, int len, CrewRole role)>();

        foreach (var (role, keys) in RoleKeys)
        {
            foreach (var k in keys)
            {
                int start = 0;
                while (true)
                {
                    int idx = s.IndexOf(k, start, StringComparison.Ordinal);
                    if (idx < 0) break;

                    marks.Add((idx, k.Length, role));
                    start = idx + k.Length;
                }
            }
        }

        // 같은 idx면 긴 키(장전수 vs 장전) 우선
        marks = marks
            .GroupBy(m => m.idx)
            .Select(g => g.OrderByDescending(x => x.len).First())
            .ToList();

        return marks;
    }

    static CrewRole InferRole(string s)
    {
        if (ContainsAny(s, Fwd) || ContainsAny(s, Back) || ContainsAny(s, Stop)) return CrewRole.Driver;
        if (ContainsAny(s, AP) || ContainsAny(s, HE) || s.Contains("장전")) return CrewRole.Loader;
        if (ContainsAny(s, Fire)) return CrewRole.Gunner;
        return CrewRole.Driver;
    }

    static List<ParsedCmd> ParseCmds(CrewRole role, string seg)
    {
        var cmds = new List<ParsedCmd>();

        if (role == CrewRole.Driver)
        {
            var intensity = ParseIntensity(seg);

            if (ContainsAny(seg, Stop))
                cmds.Add(new ParsedCmd(Cmd.Stop, Intensity.Normal));

            // 제자리 회전 우선
            if (ContainsAny(seg, PivotL))
                cmds.Add(new ParsedCmd(Cmd.PivotLeft, intensity));
            else if (ContainsAny(seg, PivotR))
                cmds.Add(new ParsedCmd(Cmd.PivotRight, intensity));
            else
            {
                if (ContainsAny(seg, TurnL))
                    cmds.Add(new ParsedCmd(Cmd.TurnLeft, intensity));
                if (ContainsAny(seg, TurnR))
                    cmds.Add(new ParsedCmd(Cmd.TurnRight, intensity));
            }

            if (ContainsAny(seg, Fwd))
                cmds.Add(new ParsedCmd(Cmd.MoveForward, intensity));
            if (ContainsAny(seg, Back))
                cmds.Add(new ParsedCmd(Cmd.MoveBackward, intensity));
        }
        else if (role == CrewRole.Gunner)
        {
            if (ContainsAny(seg, Fire))
                cmds.Add(new ParsedCmd(Cmd.Fire, Intensity.Normal));
        }
        else if (role == CrewRole.Loader)
        {
            if (ContainsAny(seg, AP))
                cmds.Add(new ParsedCmd(Cmd.LoadAP, Intensity.Normal));
            if (ContainsAny(seg, HE))
                cmds.Add(new ParsedCmd(Cmd.LoadHE, Intensity.Normal));
        }

        return cmds;
    }

    static bool ContainsAny(string s, IEnumerable<string> keys)
    {
        foreach (var k in keys)
            if (s.Contains(k)) return true;
        return false;
    }
}