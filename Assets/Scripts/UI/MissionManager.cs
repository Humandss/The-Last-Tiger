using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// 미션 진행을 관리합니다.
/// Phase 1 : 지정된 적 탱크를 모두 격파
/// Phase 2 : 긴급 지령 팝업 표시
/// Phase 3 : 증원 스폰 + 증원 격파 카운트
/// Phase 4 : 작전 완료 보고서 표시
/// </summary>
public class MissionManager : MonoBehaviour
{
    [Header("Phase 1 — 격파 목표")]
    [Tooltip("비워두면 씬의 TankAIController 전부를 자동 탐색합니다.")]
    [SerializeField] private TankAIController[] enemyTanks;
    [SerializeField] private float emergencyOrderDelay = 3f;

    [Header("Phase 2 — 긴급 지령")]
    [SerializeField] private MissionBriefingData emergencyData;

    [Header("Phase 3 — 증원 스폰")]
    [SerializeField] private GameObject[] reinforcementPrefab;
    [SerializeField] private Transform[]  spawnPoints;
    [SerializeField] private int          reinforcementCount = 6;
    [SerializeField] private float        spawnSpacing       = 30f;

    [Header("Phase 4 — 작전 완료 보고서")]
    [SerializeField] private MissionBriefingData missionCompleteData;
    [SerializeField] private float missionCompleteDelay = 3f;
    [SerializeField] private string briefingSceneName = "BriefingScene";
    [SerializeField] private float returnFadeDuration = 0.8f;

    [Header("킬 카운터 HUD")]
    [SerializeField] private bool  showKillCounter  = true;
    [SerializeField] private Color killCounterColor = new Color(0.85f, 0.72f, 0.20f, 1f);
    [SerializeField] private int   killCounterSize  = 26;

    // ── 내부 상태 ────────────────────────────────────────
    private readonly Dictionary<TankAIController, Action> enemyDestroyedHandlers = new();

    private int  _p1Target;
    private int  _p1Killed;
    private bool _phase1Done;

    private int  _p3Target;
    private int  _p3Killed;
    private bool _phase3Done;

    private Text _counterText;

    // ── 초기화 ───────────────────────────────────────────
    private void Start()
    {
        SubscribeEnemies();
        _p1Target = enemyDestroyedHandlers.Count;
        Debug.Log($"[MissionManager] Phase1 추적 대상 {_p1Target}대");
        if (showKillCounter) BuildKillCounterHUD();
        RefreshCounter();
    }

    private void OnDestroy()
    {
        UnsubscribeAll();
    }

    // ── 이벤트 구독 ──────────────────────────────────────
    private void SubscribeEnemies()
    {
        var tanks = FindObjectsOfType<TankAIController>();
        foreach (var tank in tanks)
        {
            if (tank == null || enemyDestroyedHandlers.ContainsKey(tank)) continue;
            Action handler = OnPhase1EnemyKilled;
            enemyDestroyedHandlers.Add(tank, handler);
            tank.OnEnemyTankDestroyed += handler;
        }
    }

    private void SubscribeReinforcements(TankAIController[] tanks)
    {
        foreach (var tank in tanks)
        {
            if (tank == null || enemyDestroyedHandlers.ContainsKey(tank)) continue;
            Action handler = OnPhase3EnemyKilled;
            enemyDestroyedHandlers.Add(tank, handler);
            tank.OnEnemyTankDestroyed += handler;
        }
    }

    private void UnsubscribeAll()
    {
        foreach (var pair in enemyDestroyedHandlers)
        {
            if (pair.Key != null)
                pair.Key.OnEnemyTankDestroyed -= pair.Value;
        }
        enemyDestroyedHandlers.Clear();
    }

    // ── Phase 1 콜백 ─────────────────────────────────────
    private void OnPhase1EnemyKilled()
    {
        if (_phase1Done) return;

        _p1Killed++;
        RefreshCounter();

        if (_p1Killed >= _p1Target)
        {
            _phase1Done = true;
            StartCoroutine(TriggerEmergencyOrder());
        }
    }

    // ── Phase 2 — 긴급 지령 팝업 ─────────────────────────
    private IEnumerator TriggerEmergencyOrder()
    {
        if (_counterText != null)
            _counterText.gameObject.SetActive(false);

        yield return new WaitForSeconds(emergencyOrderDelay);

        if (emergencyData == null)
        {
            Debug.LogWarning("[MissionManager] emergencyData 가 비어있습니다.");
            yield break;
        }

        var go = new GameObject("EmergencyOrderUI");
        var ui = go.AddComponent<MissionOrderUI>();
        ui.SetData(emergencyData);
        ui.OnConfirmed += SpawnReinforcements;
    }

    // ── Phase 3 — 증원 스폰 ──────────────────────────────
    private void SpawnReinforcements()
    {
        if (reinforcementPrefab == null || reinforcementPrefab.Length == 0)
        {
            Debug.LogWarning("[MissionManager] reinforcementPrefab 이 비어있습니다.");
            return;
        }
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[MissionManager] spawnPoints 가 비어있습니다.");
            return;
        }

        var spawnedTanks = new List<TankAIController>();

        if (spawnPoints.Length == 1)
        {
            Transform origin   = spawnPoints[0];
            float totalWidth   = (reinforcementCount - 1) * spawnSpacing;
            Vector3 start      = origin.position - origin.right * (totalWidth * 0.5f);

            for (int i = 0; i < reinforcementCount; i++)
            {
                Vector3 pos        = start + origin.right * (i * spawnSpacing);
                GameObject prefab  = reinforcementPrefab[i % reinforcementPrefab.Length];
                if (prefab == null) continue;

                var go  = Instantiate(prefab, pos, origin.rotation * Quaternion.Euler(0f, 225f, 0f));
                var ai  = go.GetComponent<TankAIController>();
                if (ai != null) spawnedTanks.Add(ai);
            }
        }
        else
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                GameObject prefab = reinforcementPrefab[i % reinforcementPrefab.Length];
                if (prefab == null) continue;

                var go = Instantiate(prefab, spawnPoints[i].position, spawnPoints[i].rotation);
                var ai = go.GetComponent<TankAIController>();
                if (ai != null) spawnedTanks.Add(ai);
            }
        }

        _p3Target = spawnedTanks.Count;
        _p3Killed = 0;

        SubscribeReinforcements(spawnedTanks.ToArray());

        if (_counterText != null) _counterText.gameObject.SetActive(true);
        RefreshCounter();

        Debug.Log($"[MissionManager] 증원 {_p3Target}대 스폰 완료");
    }

    // ── Phase 3 콜백 ─────────────────────────────────────
    private void OnPhase3EnemyKilled()
    {
        if (_phase3Done) return;

        _p3Killed++;
        RefreshCounter();

        if (_p3Killed >= _p3Target)
        {
            _phase3Done = true;
            StartCoroutine(TriggerMissionComplete());
        }
    }

    // ── Phase 4 — 작전 완료 보고서 ───────────────────────
    private IEnumerator TriggerMissionComplete()
    {
        if (_counterText != null)
            _counterText.gameObject.SetActive(false);

        yield return new WaitForSeconds(missionCompleteDelay);

        if (missionCompleteData == null)
        {
            Debug.LogWarning("[MissionManager] missionCompleteData 가 비어있습니다.");
            yield break;
        }

        var go = new GameObject("MissionCompleteUI");
        var ui = go.AddComponent<MissionOrderUI>();
        ui.SetData(missionCompleteData);
        ui.OnConfirmed += () => StartCoroutine(ReturnToBriefing());
    }

    // ── 브리핑 씬 복귀 ────────────────────────────────────
    private IEnumerator ReturnToBriefing()
    {
        // 검정 페이드인
        var canvasGo = new GameObject("ReturnFadeCanvas",
            typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(canvasGo);
        var canvas          = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        var cg               = canvasGo.AddComponent<CanvasGroup>();
        cg.alpha             = 0f;
        cg.blocksRaycasts    = true;
        cg.interactable      = false;

        var bgGo   = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgGo.GetComponent<UnityEngine.UI.Image>().color = Color.black;

        float t = 0f;
        while (t < returnFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / returnFadeDuration);
            yield return null;
        }
        cg.alpha = 1f;

        // 씬 로드 후 페이드아웃을 담당할 컴포넌트 부착
        canvasGo.AddComponent<ReturnFadeOut>().Init(cg, returnFadeDuration);

        SceneManager.LoadScene(briefingSceneName);
    }

    // ── 씬 로드 후 페이드아웃 컴포넌트 ───────────────────
    private class ReturnFadeOut : MonoBehaviour
    {
        private CanvasGroup _cg;
        private float       _duration;

        public void Init(CanvasGroup cg, float duration)
        {
            _cg       = cg;
            _duration = duration;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            yield return new WaitForSeconds(0.1f);
            float t = 0f;
            while (t < _duration)
            {
                t += Time.deltaTime;
                if (_cg != null) _cg.alpha = 1f - Mathf.Clamp01(t / _duration);
                yield return null;
            }
            Destroy(gameObject);
        }
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

        if (!_phase1Done)
            _counterText.text = $"◆ 격파  {_p1Killed} / {_p1Target}";
        else
            _counterText.text = $"◆ 격파  {_p3Killed} / {_p3Target}";
    }
}
