using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public enum CrewRole { Driver, Gunner, Loader }
public enum Cmd { 
    MoveForward, MoveBackward, Stop, TurnLeft, TurnRight, PivotLeft, PivotRight,
    Fire, CeaseAction, AimAt, AlignHull, SetRange, TrackTarget,
    LoadAP, LoadHE, LoadDefault
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
    private readonly Cmd cmd;
    private readonly Intensity intensity;
    private readonly float? rangeMeters;

    public ParsedCmd(Cmd cmd, Intensity intensity, float? rangeMeters = null)
    {
        this.cmd = cmd;
        this.intensity = intensity;
        this.rangeMeters = rangeMeters;
    }

    public override string ToString()
        => rangeMeters.HasValue
            ? $"{cmd}({intensity}, {rangeMeters.Value:0}m)"
            : $"{cmd}({intensity})";

    public Cmd GetCmd { get { return cmd; } }

    public float? GetRangeMeters { get { return rangeMeters; } }
}

public static class CrewParser
{
    private static readonly (CrewRole role, string[] keys)[] RoleKeys =
    {
        (CrewRole.Driver, new[] { "조종수", "조종", "운전수", "드라이버" }),
        (CrewRole.Gunner, new[] { "포수", "보수", "포스", "포주", "거너" }),
        (CrewRole.Loader, new[] { "장전수", "로더" }),
    };

    private static Intensity ParseIntensity(string seg)
    {
        if (ContainsAny(seg, LargeK)) return Intensity.Large;
        if (ContainsAny(seg, SmallK)) return Intensity.Small;
        return Intensity.Normal;
    }


    //드라이버 CMD
    private static readonly string[] Fwd = { "전진", "앞으로", "전방" };
    private static readonly string[] Back = { "후진", "뒤로", "후방", "백" };
    private static readonly string[] Stop = { "정지", "멈춰", "스톱", "서", "그만" };
    private static readonly string[] TurnL = { "좌회전", "왼쪽", "좌로" };
    private static readonly string[] TurnR = { "우회전", "오른쪽", "우로" };
    private static readonly string[] PivotL = { "제자리 좌회전", "제자리 왼쪽", "피벗 좌", "좌로 돌아" };
    private static readonly string[] PivotR = { "제자리 우회전", "제자리 오른쪽", "피벗 우", "우로 돌아" };
    // 강도
    private static readonly string[] SmallK = { "조금", "살짝", "약하게", "천천히" };
    private static readonly string[] LargeK = { "크게", "많이", "강하게", "빠르게", "빨리" };
    // 기본값 Normal
    private static readonly string[] NormalK = { "보통", "적당히", "그대로", "유지" };

    //거너 CMD
    private static readonly string[] Fire = { "발사", "사격", "격발", "쏴", "쏴라", "벌써" };
    private static readonly string[] CeaseAction = { "취소", "조준 취소", "사격 취소", "중지", "사격 중지", "기달려", "기다려", "잠깐" };
    private static readonly string[] AimKeys = { "조준", "조준해", "맞춰", "에임" };
    private static readonly string[] AlignKeys = { "정렬", "원위치", "정면", "리셋" };
    private static readonly string[] RangeKeys = { "거리", "사거리", "레인지" };
    private static readonly string[] TrackKeys = { "추적","락온", "록온", "따라가"};

    //로더 CMD
    private static readonly string[] AP = { "철갑", "ap", "철갑탄", "철갑단", "척합단" };
    private static readonly string[] HE = { "고폭", "he", "고폭탄" };
    private static readonly string[] LoadDefalut = { "장전", "장전해", "리로드", "준비", "계속" };

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

    private static string Normalize(string s)
    {
        s = s.Trim();
        s = s.Replace("!", " ").Replace(".", " ").Replace(",", " ").Replace("?", " ");
        s = Regex.Replace(s, @"\s+", " ");
        return s;
    }

    private static List<(int idx, int len, CrewRole role)> FindRoleMarks(string s)
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

    private static CrewRole InferRole(string s)
    {
        if (ContainsAny(s, Fwd) || ContainsAny(s, Back) || ContainsAny(s, Stop)) return CrewRole.Driver;

        if (ContainsAny(s, AP) || ContainsAny(s, HE) || ContainsAny(s, LoadDefalut)) return CrewRole.Loader;

        if (ContainsAny(s, Fire) || ContainsAny(s, AimKeys) || ContainsAny(s, AlignKeys) || ContainsAny(s, RangeKeys)) return CrewRole.Gunner;

        return CrewRole.Driver;
    }

    //CMD 파싱
    private static List<ParsedCmd> ParseCmds(CrewRole role, string seg)
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
            var r = TryParseRangeMeters(seg);
            if (r.HasValue)
                cmds.Add(new ParsedCmd(Cmd.SetRange, Intensity.Normal, r.Value));

            // 행동 취소
            if (ContainsAny(seg, CeaseAction))
            {
                cmds.Add(new ParsedCmd(Cmd.CeaseAction, Intensity.Normal));
                return cmds;
            }
 
            // 정렬(/리셋)
            if (ContainsAny(seg, AlignKeys))
                cmds.Add(new ParsedCmd(Cmd.AlignHull, Intensity.Normal));

            // 조준
            if (ContainsAny(seg, AimKeys))
                cmds.Add(new ParsedCmd(Cmd.AimAt, Intensity.Normal));
            
            //트래킹
            if (ContainsAny(seg, TrackKeys))
                cmds.Add(new ParsedCmd(Cmd.TrackTarget, Intensity.Normal));

            // 발사
            if (ContainsAny(seg, Fire))
                cmds.Add(new ParsedCmd(Cmd.Fire, Intensity.Normal));
        }

        else if (role == CrewRole.Loader)
        {
            bool hasAP = ContainsAny(seg, AP);
            bool hasHE = ContainsAny(seg, HE);

            if (hasAP && hasHE)
            {
                cmds.Add(new ParsedCmd(Cmd.LoadAP, Intensity.Normal));
                return cmds;
            }
            // EX: 철갑탄 장전해 -> 철갑탄 우선 할당
            if (hasAP)
            {
                cmds.Add(new ParsedCmd(Cmd.LoadAP, Intensity.Normal));
                return cmds;
            }

            if (hasHE)
            {
                cmds.Add(new ParsedCmd(Cmd.LoadHE, Intensity.Normal));
                return cmds;
            }

            // 3) 탄종이 없고 “장전”류만 있으면 기본 장전
            if (ContainsAny(seg, LoadDefalut))
            {
                cmds.Add(new ParsedCmd(Cmd.LoadDefault, Intensity.Normal));
            }
        }

        return cmds;
    }
    private static float? TryParseRangeMeters(string seg)
    {
        // 예: "거리 500", "사거리 800m", "레인지 1,200"
        var m = System.Text.RegularExpressions.Regex.Match(
            seg,
            @"(거리|사거리|레인지)\s*([0-9]{1,4}(?:,[0-9]{3})?)\s*(m|미터)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        if (!m.Success) return null;

        string num = m.Groups[2].Value.Replace(",", "");
        if (float.TryParse(num, out float v))
            return v;

        return null;
    }
    private static bool ContainsAny(string s, IEnumerable<string> keys)
    {
        foreach (var k in keys)
            if (s.Contains(k)) return true;
        return false;
    }
}