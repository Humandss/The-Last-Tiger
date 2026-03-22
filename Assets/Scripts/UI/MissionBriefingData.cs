using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Mission/Briefing Data", fileName = "MissionBriefingData")]
public class MissionBriefingData : ScriptableObject
{
    // ── 로고 ─────────────────────────────────────────────
    [Header("로고 (헤더 최상단 중앙)")]
    public Sprite logoSprite;
    public float  logoSize = 65f;   // 가로·세로 크기 (px)

    // ── 헤더 텍스트 ──────────────────────────────────────
    [Header("헤더")]
    public string classification = "최고기밀 — 독일 국방군 제4기갑군";
    public string missionName    = "작전명 : 최후의 방패";
    public string dateLocation   = "1943년 7월 12일  /  쿠르스크 남방 프로호로프카";

    // ── 상황 / 목표 ───────────────────────────────────────
    [Header("상황")]
    [TextArea(2, 4)]
    public string[] situationLines =
    {
        "아군 기갑 전력 괴멸. 전장 잔존 차량 : 1량.",
        "소련 제5근위 전차군 기갑 여단, 잔존 독일군 포위 섬멸 작전 개시.",
        "재정비 및 후퇴를 위한 시간 확보가 절박하다."
    };

    [Header("임무 목표")]
    public string[] objectives =
    {
        "적 기갑 선봉대 기습 타격",
        "아군 후퇴 완료 시까지 전선 고수",
        "생환 가능 시 후퇴하라"
    };

    // ── 전차장 발언 ───────────────────────────────────────
    [Serializable]
    public class Remark
    {
        public string speaker    = "전차장";
        [TextArea(1, 3)]
        public string line;
        public float  pauseAfter = 2.5f;
    }

    [Header("전차장 발언")]
    public Remark[] remarks =
    {
        new Remark { line = "우리가 버텨야 놈들이 산다.",               pauseAfter = 2.8f },
        new Remark { line = "연료도, 포탄도 얼마 없어. 신중하게 쏴라.", pauseAfter = 3.0f },
        new Remark { line = "...출격한다.",                             pauseAfter = 1.8f }
    };

    // ── 버튼 ─────────────────────────────────────────────
    [Header("버튼")]
    public string startButtonText = "출격";

    // ── 배경 이미지 ───────────────────────────────────────
    [Header("배경 이미지 (선택)")]
    public Sprite backgroundSprite;
    [Range(0f, 1f)]
    public float  backgroundAlpha = 0.30f;   // 낮을수록 어둡게

    // ── 지도 이미지 ───────────────────────────────────────
    [Header("지도 이미지 - 우하단 (선택)")]
    public Sprite mapSprite;
    [Range(0f, 1f)]
    public float  mapAlpha = 0.75f;

    // ── 오디오 ────────────────────────────────────────────
    [Header("오디오 — BGM")]
    public AudioClip bgmClip;
    [Range(0f, 1f)]
    public float     bgmVolume    = 0.50f;

    [Header("오디오 — 앰비언트 (포성·바람 등)")]
    public AudioClip ambientClip;
    [Range(0f, 1f)]
    public float     ambientVolume = 0.30f;

    [Header("오디오 — 타자기 SFX")]
    public AudioClip typewriterClip;
    [Range(0f, 1f)]
    public float     typewriterVolume = 0.50f;
}
