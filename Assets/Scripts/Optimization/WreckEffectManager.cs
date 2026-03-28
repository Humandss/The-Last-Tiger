using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파괴된 탱크 잔해에서 발생하는 화재/연기 이펙트를 중앙에서 관리.
/// - VFX Budget: 동시 활성 이펙트 수 하드캡, 초과 시 가장 오래된 것부터 강제 회수
/// - Distance Culling: 일정 거리 초과 이펙트 emission off
/// - Zoom 연동: 줌 배율에 따라 컬링 거리 동적 조정
/// </summary>
public class WreckEffectManager : MonoBehaviour
{
    public static WreckEffectManager Instance { get; private set; }

    [Header("Budget")]
    [Tooltip("동시에 활성화할 수 있는 최대 화재/연기 이펙트 수")]
    [SerializeField] private int maxConcurrentFires = 8;

    [Header("Culling")]
    [Tooltip("줌 아웃 기준 컬링 거리 (m)")]
    [SerializeField] private float baseCullDistance = 100f;
    [Tooltip("EvaluateAll 호출 주기 (초)")]
    [SerializeField] private float evalInterval = 1f;

    // ── 내부 상태 ──────────────────────────────────────────

    private float currentCullDistance;
    private Camera mainCam;
    private bool prevIsZooming;

    private readonly List<FireEntry> activeFires = new List<FireEntry>();

    private class FireEntry
    {
        public GameObject vfx;
        public Transform  anchor;     // 잔해 위치 추적용
        public bool       isEmitting; // 현재 emission 상태
    }

    // ── 초기화 ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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

    // ── 줌 변경 감지 (즉시 재평가) ─────────────────────────

    private void Update()
    {
        if (CameraController.cameraInstance == null) return;

        bool isZooming = CameraController.cameraInstance.IsZooming;
        if (isZooming == prevIsZooming) return;

        prevIsZooming = isZooming;
        EvaluateAll();
    }

    // ── 주기적 평가 루프 ───────────────────────────────────

    private IEnumerator EvalLoop()
    {
        var wait = new WaitForSeconds(evalInterval);
        while (true)
        {
            yield return wait;
            EvaluateAll();
        }
    }

    // ── Public API ─────────────────────────────────────────

    /// <summary>
    /// 화재/연기 이펙트 스폰 후 등록. TankFireEffects, AmmoRackEffects에서 호출.
    /// </summary>
    public void Register(GameObject vfx, Transform anchor)
    {
        if (vfx == null) return;

        activeFires.Add(new FireEntry
        {
            vfx        = vfx,
            anchor     = anchor,
            isEmitting = true
        });

        // 버젯 초과 시 가장 먼저 스폰된 이펙트 강제 회수
        if (activeFires.Count > maxConcurrentFires)
            Evict();
    }

    /// <summary>
    /// 화재/연기 이펙트 반환 전 등록 해제. StopFire에서 호출.
    /// </summary>
    public void Unregister(GameObject vfx)
    {
        if (vfx == null) return;
        activeFires.RemoveAll(e => e.vfx == vfx);
    }

    // ── 핵심 로직 ──────────────────────────────────────────

    private void EvaluateAll()
    {
        if (mainCam == null && CameraController.cameraInstance != null)
            mainCam = CameraController.cameraInstance.Cam;

        activeFires.RemoveAll(e => e.vfx == null || e.anchor == null);

        if (activeFires.Count == 0) return;

        UpdateCullDistance();

        Vector3 playerPos = mainCam != null ? mainCam.transform.position : Vector3.zero;

        for (int i = 0; i < activeFires.Count; i++)
        {
            FireEntry entry = activeFires[i];
            float dist = Vector3.Distance(playerPos, entry.anchor.position);
            SetEmission(entry, dist <= currentCullDistance);
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

        float zoomMag = Mathf.Max(1f, CameraController.cameraInstance.BaseFov
                                      / Mathf.Max(1f, mainCam.fieldOfView));

        currentCullDistance = baseCullDistance * zoomMag;
    }

    private void Evict()
    {
        FireEntry evicted = activeFires[0];
        activeFires.RemoveAt(0);

        if (evicted.vfx != null)
        {
            evicted.vfx.transform.SetParent(null);
            PoolManager.Instance.Return(evicted.vfx);
        }
    }

    private void SetEmission(FireEntry entry, bool enable)
    {
        if (entry.isEmitting == enable) return;
        entry.isEmitting = enable;

        ParticleSystem[] psList = entry.vfx.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in psList)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = enable;
        }
    }
}
