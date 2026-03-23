using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미션 진행을 관리합니다.
/// Phase 1 : 추적 중인 적 탱크를 모두 격파 (TankAIController.enabled == false 감지)
/// Phase 2 : 긴급 지령 팝업 표시
/// </summary>
public class MissionManager : MonoBehaviour
{
    [Header("Phase 1 — 격파 목표")]
    [Tooltip("비워두면 씬의 TankAIController 전부를 자동 탐색합니다.")]
    [SerializeField] private TankAIController[] enemyTanks;
    [SerializeField] private float emergencyOrderDelay = 3f;

    [Header("Phase 2 — 긴급 지령")]
    [Tooltip("Assets/ 우클릭 → Create → Mission → Briefing Data 로 생성")]
    [SerializeField] private MissionBriefingData emergencyData;

    [Header("킬 카운터 HUD")]
    [SerializeField] private bool  showKillCounter  = true;
    [SerializeField] private Color killCounterColor = new Color(0.85f, 0.72f, 0.20f, 1f);
    [SerializeField] private int   killCounterSize  = 26;

    private int  _targetCount;
    private int  _killed;
    private bool _phase1Done;
    private Text _counterText;

    // 인덱스 기반 카운팅 — tank 파괴(null) 후에도 누락 없이 감지
    private bool[] _tankCounted;

    // ─────────────────────────────────────────────────────
    private void Start()
    {
        if (enemyTanks == null || enemyTanks.Length == 0)
            enemyTanks = FindObjectsOfType<TankAIController>();

        // 중복 제거 및 null 제거
        var valid = new List<TankAIController>();
        var seen  = new HashSet<TankAIController>();
        foreach (var t in enemyTanks)
            if (t != null && seen.Add(t)) valid.Add(t);
        enemyTanks = valid.ToArray();

        _targetCount  = enemyTanks.Length;
        _tankCounted  = new bool[_targetCount];
        Debug.Log($"[MissionManager] 추적 대상 {_targetCount}대");

        if (showKillCounter) BuildKillCounterHUD();
        RefreshCounter();
    }

    // ── 폴링 방식 격파 감지 ───────────────────────────────
    // null  = GameObject 파괴됨 = 격파 확정
    // !enabled = Die() 직후 = 격파 확정
    private void Update()
    {
        if (_phase1Done || _tankCounted == null) return;

        for (int i = 0; i < enemyTanks.Length; i++)
        {
            if (_tankCounted[i]) continue;

            var tank   = enemyTanks[i];
            bool isDead = tank == null || !tank.enabled;   // 파괴됐거나 Die() 호출됨
            if (!isDead) continue;

            _tankCounted[i] = true;
            _killed++;
            RefreshCounter();
            string tankName = tank != null ? tank.name : $"Tank[{i}]";
            Debug.Log($"[MissionManager] 격파 {_killed}/{_targetCount}: {tankName}");

            if (_killed >= _targetCount)
            {
                _phase1Done = true;
                StartCoroutine(TriggerEmergencyOrder());
                break;
            }
        }
    }

    // ── 긴급 지령 팝업 ────────────────────────────────────
    private IEnumerator TriggerEmergencyOrder()
    {
        if (_counterText != null)
            _counterText.gameObject.SetActive(false);

        yield return new WaitForSeconds(emergencyOrderDelay);

        if (emergencyData == null)
        {
            Debug.LogWarning("[MissionManager] emergencyData 가 비어있습니다. Inspector 에서 할당하세요.");
            yield break;
        }

        var go = new GameObject("EmergencyOrderUI");
        var ui = go.AddComponent<MissionOrderUI>();
        ui.SetData(emergencyData);
    }

    // ── 킬 카운터 HUD ────────────────────────────────────
    private void BuildKillCounterHUD()
    {
        var canvasGo = new GameObject("KillCounterCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas          = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;

        var scaler                 = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        var textGo = new GameObject("KillCounter", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);

        var rect              = textGo.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(1f, 1f);
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(1f, 1f);
        rect.sizeDelta        = new Vector2(260f, 60f);
        rect.anchoredPosition = new Vector2(-28f, -28f);

        _counterText               = textGo.GetComponent<Text>();
        _counterText.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _counterText.fontSize      = killCounterSize;
        _counterText.fontStyle     = FontStyle.Bold;
        _counterText.color         = killCounterColor;
        _counterText.alignment     = TextAnchor.UpperRight;
        _counterText.raycastTarget = false;
    }

    private void RefreshCounter()
    {
        if (_counterText == null) return;
        _counterText.text = $"◆ 격파  {_killed} / {_targetCount}";
    }
}
