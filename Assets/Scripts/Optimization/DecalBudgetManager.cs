using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피격 데칼을 중앙에서 버젯 + 거리 컬링으로 관리.
/// - 동시 활성 데칼 수 하드캡 (LRU eviction)
/// - 일정 거리 초과 데칼은 SetActive(false) (드로우콜 절약)
/// - 줌 배율에 따라 컬링 거리 동적 조정 (스코프로 멀리 보일 때)
///
/// WreckEffectManager 패턴 참고.
/// </summary>
public class DecalBudgetManager : MonoBehaviour
{
    public static DecalBudgetManager Instance { get; private set; }

    [Header("Budget")]
    [Tooltip("동시에 활성화할 수 있는 최대 데칼 수")]
    [SerializeField] private int maxConcurrentDecals = 50;

    [Header("Culling")]
    [Tooltip("줌 아웃 기준 컬링 거리 (m)")]
    [SerializeField] private float baseCullDistance = 100f;
    [Tooltip("EvaluateAll 호출 주기 (초)")]
    [SerializeField] private float evalInterval = 1f;

    // ── 내부 상태 ─────────────────────────────────────────

    private float currentCullDistance;
    private Camera mainCam;
    private bool prevIsZooming;

    private readonly List<DecalEntry> activeDecals = new List<DecalEntry>();

    private class DecalEntry
    {
        public GameObject go;
        public bool isVisible;
    }

    // ── 초기화 ────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentCullDistance = baseCullDistance;
    }

    private void Start()
    {
        if (CameraController.cameraInstance != null)
            mainCam = CameraController.cameraInstance.Cam;

        StartCoroutine(EvalLoop());
    }

    // ── 줌 변경 감지 (즉시 재평가) ────────────────────────

    private void Update()
    {
        if (CameraController.cameraInstance == null) return;

        bool isZooming = CameraController.cameraInstance.IsZooming;
        if (isZooming == prevIsZooming) return;

        prevIsZooming = isZooming;
        EvaluateAll();
    }

    // ── 주기적 평가 루프 ──────────────────────────────────

    private IEnumerator EvalLoop()
    {
        var wait = new WaitForSeconds(evalInterval);
        while (true)
        {
            yield return wait;
            EvaluateAll();
        }
    }

    // ── Public API ────────────────────────────────────────

    /// <summary>
    /// 새로 스폰된 데칼을 등록.
    /// 버젯 초과 시 가장 오래된 것부터 풀에 반환.
    /// </summary>
    public void Register(GameObject decal)
    {
        if (decal == null) return;

        activeDecals.Add(new DecalEntry
        {
            go = decal,
            isVisible = true,
        });

        while (activeDecals.Count > maxConcurrentDecals)
            EvictOldest();
    }

    /// <summary>
    /// 외부에서 명시적으로 제거할 때 (보통 호출 안 함).
    /// </summary>
    public void Unregister(GameObject decal)
    {
        if (decal == null) return;
        activeDecals.RemoveAll(e => e.go == decal);
    }

    // ── 핵심 로직 ─────────────────────────────────────────

    private void EvaluateAll()
    {
        if (mainCam == null && CameraController.cameraInstance != null)
            mainCam = CameraController.cameraInstance.Cam;

        // 죽은 참조 청소
        activeDecals.RemoveAll(e => e.go == null);

        if (activeDecals.Count == 0) return;

        UpdateCullDistance();

        Vector3 playerPos = mainCam != null ? mainCam.transform.position : Vector3.zero;

        for (int i = 0; i < activeDecals.Count; i++)
        {
            DecalEntry entry = activeDecals[i];
            float dist = Vector3.Distance(playerPos, entry.go.transform.position);
            SetVisible(entry, dist <= currentCullDistance);
        }
    }

    private void UpdateCullDistance()
    {
        if (mainCam == null ||
            CameraController.cameraInstance == null ||
            !CameraController.cameraInstance.IsZooming)
        {
            currentCullDistance = baseCullDistance;
            return;
        }

        // 줌 배율만큼 컬링 거리 늘려줌 (스코프로 멀리 봐도 데칼 보이도록)
        float zoomMag = Mathf.Max(1f, CameraController.cameraInstance.BaseFov
                                      / Mathf.Max(1f, mainCam.fieldOfView));

        currentCullDistance = baseCullDistance * zoomMag;
    }

    private void SetVisible(DecalEntry entry, bool visible)
    {
        if (entry.isVisible == visible) return;
        entry.isVisible = visible;

        if (entry.go != null)
            entry.go.SetActive(visible);
    }

    private void EvictOldest()
    {
        if (activeDecals.Count == 0) return;

        DecalEntry oldest = activeDecals[0];
        activeDecals.RemoveAt(0);

        if (oldest.go == null) return;

        oldest.go.transform.SetParent(null);
        PoolManager.Instance.Return(oldest.go);
    }

    // ── 디버그 ────────────────────────────────────────────

    public int CurrentDecalCount => activeDecals.Count;
    public int MaxDecalCount => maxConcurrentDecals;
    public float CurrentCullDistance => currentCullDistance;
}
