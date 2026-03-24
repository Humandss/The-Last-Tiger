using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 미션 진행을 관리합니다.
/// Phase 1 : 지정된 적 탱크를 모두 격파
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

    [Header("Startup Sequence")]
    private readonly Dictionary<TankAIController, Action> enemyDestroyedHandlers = new();

    private int  _targetCount;
    private int  _killed;
    private bool _phase1Done;
    private Text _counterText;

    private void Start()
    {
        SubscribeEnemyDestroyedCount();
        _targetCount = enemyDestroyedHandlers.Count;
        Debug.Log($"[MissionManager] 추적 대상 {_targetCount}대");
        if (showKillCounter) BuildKillCounterHUD();
        RefreshCounter();
    }

    private void OnDestroy()
    {
        UnSubscribeEnemyDestroyedCount();
    }

    private void SubscribeEnemyDestroyedCount()
    {
        var enemyTanks = FindObjectsOfType<TankAIController>();
        for (int i = 0; i < enemyTanks.Length; i++)
        {
            var tank = enemyTanks[i];
            if (tank == null || enemyDestroyedHandlers.ContainsKey(tank))
                continue;

            Action handler = OnEnemyKilled;
            enemyDestroyedHandlers.Add(tank, handler);
            tank.OnEnemyTankDestroyed += handler;
            Debug.Log($"subscribe type : {tank.gameObject.name}");
        }
    }
    private void UnSubscribeEnemyDestroyedCount()
    {
        foreach (var pair in enemyDestroyedHandlers)
        {
            if (pair.Key == null)
                continue;

            pair.Key.OnEnemyTankDestroyed -= pair.Value;
        }
        enemyDestroyedHandlers.Clear();
    }
    // ── static 이벤트 콜백 ────────────────────────────────
    private void OnEnemyKilled()
    {
        Debug.LogWarning($"Enemy Tank Destroyed");
        if (_phase1Done) return;

        _killed++;
        RefreshCounter();

        if (_killed >= _targetCount)
        {
            _phase1Done = true;
            StartCoroutine(TriggerEmergencyOrder());
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
