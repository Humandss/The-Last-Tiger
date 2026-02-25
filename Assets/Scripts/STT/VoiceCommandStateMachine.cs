using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class VoiceCommandStateMachine : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float commandWindowSeconds = 2.0f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private bool hasActiveRole;
    private CrewRole activeRole;
    private float roleWindowExpireTime;

    // 외부(네 STT 파이프라인)에서 이 이벤트 받아서 실제 게임 실행하면 됨
    public event Action<CrewRole, ParsedCmd> OnCommandAccepted;

    private void Update()
    {
        if (hasActiveRole && Time.time > roleWindowExpireTime)
        {
            if (debugLog) Debug.Log($"[VoiceSM] Role window expired: {activeRole}");
            ClearRoleWindow();
        }
    }

    /// <summary>
    /// STT 결과 텍스트를 넣어 처리한다.
    /// 예: "운전수", "출발", "포수 조준", "장전수 철갑탄 장전"
    /// </summary>
    public void ProcessSttText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return;

        string text = rawText.Trim();

        if (debugLog) Debug.Log($"[VoiceSM] STT IN: {text}");

        // 0) 완전 쓰레기/인사말 컷 (필요 시 확장)
        if (IsGarbage(text))
        {
            if (debugLog) Debug.Log($"[VoiceSM] Garbage ignored: {text}");
            return;
        }

        // 1) 역할+명령이 한 번에 들어온 경우 먼저 처리 시도 (예: "포수 조준", "운전수 출발")
        if (TryParseRoleAndCmdTogether(text, out CrewRole combinedRole, out ParsedCmd combinedCmd))
        {
            AcceptCommand(combinedRole, combinedCmd, "combined");
            return;
        }

        // 2) 역할 호출만 들어온 경우 (예: "운전수", "포수")
        if (TryParseRoleOnly(text, out CrewRole role))
        {
            OpenRoleWindow(role);
            return;
        }

        // 3) 역할 창이 열려 있으면 해당 역할의 명령으로만 해석
        if (hasActiveRole)
        {
            if (TryParseCommandForActiveRole(text, activeRole, out ParsedCmd cmd))
            {
                AcceptCommand(activeRole, cmd, "role-window");
                return;
            }

            if (debugLog) Debug.Log($"[VoiceSM] No valid cmd for active role={activeRole}: {text}");
            return;
        }

        // 4) 역할 창도 없고 역할도 안 들렸으면 무시 (안전)
        if (debugLog) Debug.Log($"[VoiceSM] Ignored (no role context): {text}");
    }

    private void OpenRoleWindow(CrewRole role)
    {
        hasActiveRole = true;
        activeRole = role;
        roleWindowExpireTime = Time.time + commandWindowSeconds;

        if (debugLog) Debug.Log($"[VoiceSM] Role selected: {activeRole} ({commandWindowSeconds:0.0}s)");
        // TODO: UI/음성 피드백 "운전수, 명령 대기"
    }

    private void ClearRoleWindow()
    {
        hasActiveRole = false;
    }

    private void AcceptCommand(CrewRole role, ParsedCmd cmd, string reason)
    {
        // 명령 실행 후 역할창 유지 여부는 취향/명령 타입 따라 조정 가능
        // 일단은 단순하게 유지 (연속 명령 가능)
        roleWindowExpireTime = Time.time + commandWindowSeconds;

        if (debugLog) Debug.Log($"[VoiceSM] ACCEPT ({reason}) {role} -> {cmd}");

        OnCommandAccepted?.Invoke(role, cmd);

        // TODO: 피드백 "포수, 조준"
    }

    // -------------------------
    // Parsing helpers (최소 버전)
    // -------------------------

    private bool TryParseRoleOnly(string text, out CrewRole role)
    {
        string t = text.Replace(" ", "");

        if (ContainsAny(t, "운전수", "조종수", "조종", "드라이버"))
        {
            role = CrewRole.Driver;
            return true;
        }

        if (ContainsAny(t, "포수", "거너", "보수", "포스", "포주"))
        {
            role = CrewRole.Gunner;
            return true;
        }

        if (ContainsAny(t, "장전수", "로더"))
        {
            role = CrewRole.Loader;
            return true;
        }

        role = default;
        return false;
    }

    private bool TryParseRoleAndCmdTogether(string text, out CrewRole role, out ParsedCmd cmd)
    {
        // 네 기존 CrewParser를 활용
        var dict = CrewParser.Parse(text);

        foreach (var kv in dict)
        {
            if (kv.Value != null && kv.Value.Count > 0)
            {
                role = kv.Key;
                cmd = kv.Value[0]; // 일단 첫 명령만
                return true;
            }
        }

        role = default;
        cmd = default;
        return false;
    }

    private bool TryParseCommandForActiveRole(string text, CrewRole role, out ParsedCmd cmd)
    {
        // "포수 조준"처럼 역할명이 섞여 들어와도 되게 역할 prefix 붙여서 파싱해도 되고,
        // 여기선 간단히 역할별 최소 키워드 파싱으로 감.
        // (나중에 CrewParser.ParseCmds를 public/internal로 분리하면 더 깔끔)

        string t = text.Replace(" ", "");
        Intensity intensity = ParseIntensity(t);

        switch (role)
        {
            case CrewRole.Driver:
                {
                    if (ContainsAny(t, "정지", "멈춰", "스톱", "그만"))
                    {
                        cmd = new ParsedCmd(Cmd.Stop, Intensity.Normal);
                        return true;
                    }

                    if (ContainsAny(t, "제자리좌회전", "피벗좌"))
                    {
                        cmd = new ParsedCmd(Cmd.PivotLeft, intensity);
                        return true;
                    }

                    if (ContainsAny(t, "제자리우회전", "피벗우"))
                    {
                        cmd = new ParsedCmd(Cmd.PivotRight, intensity);
                        return true;
                    }

                    if (ContainsAny(t, "좌회전", "왼쪽", "좌로"))
                    {
                        cmd = new ParsedCmd(Cmd.TurnLeft, intensity);
                        return true;
                    }

                    if (ContainsAny(t, "우회전", "오른쪽", "우로"))
                    {
                        cmd = new ParsedCmd(Cmd.TurnRight, intensity);
                        return true;
                    }

                    if (ContainsAny(t, "전진", "앞으로", "출발"))
                    {
                        cmd = new ParsedCmd(Cmd.MoveForward, intensity);
                        return true;
                    }

                    if (ContainsAny(t, "후진", "뒤로", "백"))
                    {
                        cmd = new ParsedCmd(Cmd.MoveBackward, intensity);
                        return true;
                    }

                    break;
                }

            case CrewRole.Gunner:
                {
                    // 거리 먼저
                    float? range = TryParseRangeMetersSimple(text);
                    if (range.HasValue)
                    {
                        cmd = new ParsedCmd(Cmd.SetRange, Intensity.Normal, range.Value);
                        return true;
                    }

                    if (ContainsAny(t, "취소", "중지", "잠깐", "기다려", "기달려"))
                    {
                        cmd = new ParsedCmd(Cmd.CeaseAction, Intensity.Normal);
                        return true;
                    }

                    if (ContainsAny(t, "정렬", "원위치", "정면", "리셋"))
                    {
                        cmd = new ParsedCmd(Cmd.AlignHull, Intensity.Normal);
                        return true;
                    }

                    if (ContainsAny(t, "조준", "조준해", "맞춰", "에임", "조즌", "조순", "초준"))
                    {
                        cmd = new ParsedCmd(Cmd.AimAt, Intensity.Normal);
                        return true;
                    }

                    if (ContainsAny(t, "추적", "락온", "록온", "따라가"))
                    {
                        cmd = new ParsedCmd(Cmd.TrackTarget, Intensity.Normal);
                        return true;
                    }

                    if (ContainsAny(t, "발사", "사격", "격발", "쏴", "벌써"))
                    {
                        cmd = new ParsedCmd(Cmd.Fire, Intensity.Normal);
                        return true;
                    }

                    break;
                }

            case CrewRole.Loader:
                {
                    if (ContainsAny(t, "철갑", "철갑탄", "ap", "철갑단", "척합단"))
                    {
                        cmd = new ParsedCmd(Cmd.LoadAP, Intensity.Normal);
                        return true;
                    }

                    if (ContainsAny(t, "고폭", "고폭탄", "he"))
                    {
                        cmd = new ParsedCmd(Cmd.LoadHE, Intensity.Normal);
                        return true;
                    }

                    if (ContainsAny(t, "장전", "장전해", "리로드", "준비", "계속"))
                    {
                        cmd = new ParsedCmd(Cmd.LoadDefault, Intensity.Normal);
                        return true;
                    }

                    break;
                }
        }

        cmd = default;
        return false;
    }

    private bool IsGarbage(string text)
    {
        string t = text.Replace(" ", "");
        return ContainsAny(t, "감사합니다", "안녕하세요", "죄송합니다", "부탁드립니다");
    }

    private Intensity ParseIntensity(string t)
    {
        if (ContainsAny(t, "크게", "많이", "강하게", "빠르게", "빨리")) return Intensity.Large;
        if (ContainsAny(t, "조금", "살짝", "약하게", "천천히")) return Intensity.Small;
        return Intensity.Normal;
    }

    private float? TryParseRangeMetersSimple(string text)
    {
        // 네 CrewParser regex 재사용 가능하면 그게 베스트
        var m = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(거리|사거리|레인지)\s*([0-9]{1,4}(?:,[0-9]{3})?)\s*(m|미터)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        if (!m.Success) return null;

        string num = m.Groups[2].Value.Replace(",", "");
        if (float.TryParse(num, out float v)) return v;
        return null;
    }

    private bool ContainsAny(string s, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (s.IndexOf(keys[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }
}
