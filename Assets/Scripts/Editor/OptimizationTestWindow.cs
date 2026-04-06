using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEngine;
using SW = System.Diagnostics.Stopwatch;

/// <summary>
/// 최적화 시스템(PoolManager / WreckEffectManager / 음속 딜레이) 자동 검증 툴.
/// Unity 메뉴 Tools → 최적화 테스트 로 실행.
/// </summary>
public class OptimizationTestWindow : EditorWindow
{
    // ── 탭 ────────────────────────────────────────────────────
    private int   _tab;
    private static readonly string[] Tabs = { "PoolManager", "WreckFX", "전체 실행" };

    // ── Pool 설정 ─────────────────────────────────────────────
    private GameObject _spawnPrefab;
    private int        _spawnN = 1000;

    // ── WreckFX 벤치마크 설정 ─────────────────────────────────
    private const int WreckBenchmarkPrefabSlots = 4;
    private readonly GameObject[] _wreckBenchmarkPrefabs = new GameObject[WreckBenchmarkPrefabSlots];
    private float _wreckBenchmarkSeconds = 3f;
    private int _wreckBenchmarkRepeats = 20;

    // ── 결과 로그 ─────────────────────────────────────────────
    private readonly List<TestResult> _results = new();
    private Vector2 _scroll;

    private struct TestResult
    {
        public bool   pass;
        public string label;
        public string detail;
    }

    private struct PerfSample
    {
        public double ms;
        public long managedBytes;
    }

    private struct FrameSample
    {
        public float averageMs;
        public float minMs;
        public float maxMs;
        public float averageFps;
        public int frames;
        public long managedDelta;
    }

    private struct FrameAggregate
    {
        public float averageMs;
        public float averageFps;
        public float minMs;
        public float maxMs;
        public long averageManagedDelta;
        public int runs;
    }

    private enum WreckBenchmarkMode
    {
        None,
        BudgetOnly,
        DistanceOnly,
        Both
    }

    private bool _wreckFrameBenchmarkRunning;
    private int _wreckBenchmarkModeIndex;
    private int _wreckBenchmarkCurrentRepeat;
    private double _wreckFrameBenchmarkEndsAt;
    private long _wreckFrameMemoryStart;
    private float _wreckFrameTotalDt;
    private float _wreckFrameMinDt;
    private float _wreckFrameMaxDt;
    private int _wreckFrameCount;
    private float _wreckBenchmarkBaseCullOriginal;
    private int _wreckBenchmarkMaxOriginal;
    private readonly List<GameObject> _wreckBenchmarkObjects = new();
    private readonly FrameSample[] _wreckBenchmarkSamples = new FrameSample[4];
    private readonly List<FrameSample>[] _wreckBenchmarkRuns =
    {
        new List<FrameSample>(),
        new List<FrameSample>(),
        new List<FrameSample>(),
        new List<FrameSample>()
    };

    // ── 리플렉션 캐시 ─────────────────────────────────────────
    // PoolManager
    private static readonly FieldInfo FPrefabToPool   = typeof(PoolManager).GetField("prefabToPool",   BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo FInstanceToPrefab = typeof(PoolManager).GetField("instanceToPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo FSoundPool      = typeof(PoolManager).GetField("soundPool",      BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo FActiveSounds   = typeof(PoolManager).GetField("activeSounds",   BindingFlags.NonPublic | BindingFlags.Instance);

    // WreckEffectManager
    private static readonly FieldInfo FActiveFires    = typeof(WreckEffectManager).GetField("activeFires",       BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo FMaxFires       = typeof(WreckEffectManager).GetField("maxConcurrentFires", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo FBaseCull       = typeof(WreckEffectManager).GetField("baseCullDistance",   BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo FCurrentCull    = typeof(WreckEffectManager).GetField("currentCullDistance", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo MEvaluateAll   = typeof(WreckEffectManager).GetMethod("EvaluateAll", BindingFlags.NonPublic | BindingFlags.Instance);

    // ── 메뉴 엔트리 ───────────────────────────────────────────
    [MenuItem("Tools/최적화 테스트 &t")]
    public static void Open() => GetWindow<OptimizationTestWindow>("최적화 테스트");

    // ── OnGUI ─────────────────────────────────────────────────
    private void OnGUI()
    {
        GUILayout.Label("⚙  최적화 시스템 자동 테스트", EditorStyles.boldLabel);

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("런타임 테스트는 플레이 모드에서만 실행됩니다. " +
                                    "음속 수식 검증은 에디터 모드에서도 가능합니다.", MessageType.Info);

        EditorGUILayout.Space(2);
        _tab = GUILayout.Toolbar(_tab, Tabs);
        EditorGUILayout.Space(4);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        switch (_tab)
        {
            case 0: DrawPoolTab();  break;
            case 1: DrawWreckTab(); break;
            case 2: DrawAllTab();   break;
        }

        EditorGUILayout.Space(8);
        DrawResults();
        EditorGUILayout.EndScrollView();
    }

    private void Update()
    {
        if (!_wreckFrameBenchmarkRunning || !Application.isPlaying)
            return;

        SampleWreckBenchmarkFrame();
    }

    private void OnDisable()
    {
        RestoreWreckBenchmarkOriginals(WreckEffectManager.Instance);
    }

    // ══════════════════════════════════════════════════════════
    // TAB 0 — PoolManager
    // ══════════════════════════════════════════════════════════
    private void DrawPoolTab()
    {
        GUILayout.Label("PoolManager", EditorStyles.boldLabel);
        _spawnPrefab = (GameObject)EditorGUILayout.ObjectField("테스트 프리팹", _spawnPrefab, typeof(GameObject), false);
        _spawnN      = EditorGUILayout.IntSlider("스폰 수", _spawnN, 10, 500);

        bool canRun = Application.isPlaying && _spawnPrefab != null && PoolManager.Instance != null;
        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button("스폰 / 반환 속도 측정"))   RunTest(TestPoolSpawnReturn);
            if (GUILayout.Button("Pool vs Instantiate 비교")) RunTest(TestPoolVsInstantiate);
            if (GUILayout.Button("Before / After 비교"))      RunTest(TestPoolBeforeAfterComparison);
            if (GUILayout.Button("Pool 상태 스냅샷"))         RunTest(TestPoolSnapshot);
        }

        if (!Application.isPlaying || PoolManager.Instance == null) return;

        EditorGUILayout.Space(4);
        GUILayout.Label("── 현재 Pool 상태 ──", EditorStyles.miniLabel);
        DrawPoolLiveStats();
    }

    private void DrawPoolLiveStats()
    {
        var pm = PoolManager.Instance;

        // GameObject pool
        if (FPrefabToPool?.GetValue(pm) is System.Collections.IDictionary ptop)
        {
            int totalIdle = 0;
            var keys = ptop.Keys;
            foreach (var key in keys)
            {
                if (ptop[key] is System.Collections.ICollection q)
                    totalIdle += q.Count;
            }
            EditorGUILayout.LabelField("오브젝트 풀 종류", ptop.Count.ToString());
            EditorGUILayout.LabelField("대기 중인 오브젝트 합계", totalIdle.ToString());
        }

        if (FInstanceToPrefab?.GetValue(pm) is System.Collections.IDictionary itp)
            EditorGUILayout.LabelField("현재 활성 오브젝트", itp.Count.ToString());

        // Sound pool
        if (FSoundPool?.GetValue(pm) is System.Collections.ICollection sp)
            EditorGUILayout.LabelField("대기 AudioSource", sp.Count.ToString());

        if (FActiveSounds?.GetValue(pm) is System.Collections.ICollection sa)
            EditorGUILayout.LabelField("활성 AudioSource", sa.Count.ToString());
    }

    private void TestPoolSpawnReturn()
    {
        var pm      = PoolManager.Instance;
        var spawned = new List<GameObject>(_spawnN);

        var sw = SW.StartNew();
        for (int i = 0; i < _spawnN; i++)
            spawned.Add(pm.Spawn(_spawnPrefab, Vector3.zero, Quaternion.identity));
        sw.Stop();
        double spawnMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        foreach (var go in spawned) pm.Return(go);
        sw.Stop();
        double returnMs = sw.Elapsed.TotalMilliseconds;

        Log(true,                   "스폰 시간",   $"{_spawnN}개 → {spawnMs:F2} ms");
        Log(true,                   "반환 시간",   $"{_spawnN}개 → {returnMs:F2} ms");
        Log(spawned.Count == _spawnN, "수량 검증", $"요청 {_spawnN} / 실제 {spawned.Count}");
    }

    private void TestPoolVsInstantiate()
    {
        var pm      = PoolManager.Instance;
        var spawned = new List<GameObject>(_spawnN);

        // 풀 워밍업 (첫 Instantiate 포함 비용 제거)
        for (int i = 0; i < _spawnN; i++)
            spawned.Add(pm.Spawn(_spawnPrefab, Vector3.zero, Quaternion.identity));
        foreach (var go in spawned) pm.Return(go);
        spawned.Clear();

        // Pool 재사용 측정
        var sw = SW.StartNew();
        for (int i = 0; i < _spawnN; i++)
            spawned.Add(pm.Spawn(_spawnPrefab, Vector3.zero, Quaternion.identity));
        sw.Stop();
        double poolMs = sw.Elapsed.TotalMilliseconds;
        foreach (var go in spawned) pm.Return(go);

        // 일반 Instantiate 측정
        var insts = new List<GameObject>(_spawnN);
        sw.Restart();
        for (int i = 0; i < _spawnN; i++)
            insts.Add(Instantiate(_spawnPrefab));
        sw.Stop();
        double instMs = sw.Elapsed.TotalMilliseconds;
        foreach (var go in insts) DestroyImmediate(go);

        bool   faster = poolMs < instMs;
        double ratio  = instMs > 0.001 ? instMs / poolMs : 1.0;
        Log(faster, "Pool vs Instantiate",
            $"Pool {poolMs:F2}ms  /  Instantiate {instMs:F2}ms  →  Pool이 {ratio:F1}x {(faster ? "빠름 ✓" : "느림 ✗")}");
    }

    private void TestPoolSnapshot()
    {
        var pm = PoolManager.Instance;

        int idle   = 0;
        int active = 0;
        int kinds  = 0;

        if (FPrefabToPool?.GetValue(pm) is System.Collections.IDictionary ptop)
        {
            kinds = ptop.Count;
            foreach (var key in ptop.Keys)
                if (ptop[key] is System.Collections.ICollection q) idle += q.Count;
        }
        if (FInstanceToPrefab?.GetValue(pm) is System.Collections.IDictionary itp)
            active = itp.Count;

        Log(true, "Pool 스냅샷",
            $"풀 종류: {kinds}  /  대기: {idle}  /  활성: {active}  /  총: {idle + active}");
    }

    private void TestPoolBeforeAfterComparison()
    {
        var pm = PoolManager.Instance;
        if (pm == null || _spawnPrefab == null)
        {
            Log(false, "Before / After", "PoolManager 또는 테스트 프리팹이 없음");
            return;
        }

        var warmups = new List<GameObject>(_spawnN);
        for (int i = 0; i < _spawnN; i++)
            warmups.Add(pm.Spawn(_spawnPrefab, Vector3.zero, Quaternion.identity));
        foreach (var go in warmups)
            pm.Return(go);

        PerfSample afterOpt = MeasurePerf(() =>
        {
            var spawned = new List<GameObject>(_spawnN);
            for (int i = 0; i < _spawnN; i++)
                spawned.Add(pm.Spawn(_spawnPrefab, Vector3.zero, Quaternion.identity));
            foreach (var go in spawned)
                pm.Return(go);
        });

        PerfSample beforeOpt = MeasurePerf(() =>
        {
            var created = new List<GameObject>(_spawnN);
            for (int i = 0; i < _spawnN; i++)
                created.Add(Instantiate(_spawnPrefab));
            foreach (var go in created)
                DestroyImmediate(go);
        });

        double speedup = afterOpt.ms > 0.001 ? beforeOpt.ms / afterOpt.ms : 1.0;
        long memorySaved = beforeOpt.managedBytes - afterOpt.managedBytes;
        bool faster = afterOpt.ms < beforeOpt.ms;

        Log(true, "Before (비최적화)",
            $"Instantiate/Destroy {_spawnN}회  {beforeOpt.ms:F2} ms  /  managed Δ {FormatBytes(beforeOpt.managedBytes)}");
        Log(true, "After (최적화)",
            $"Pool Spawn/Return {_spawnN}회  {afterOpt.ms:F2} ms  /  managed Δ {FormatBytes(afterOpt.managedBytes)}");
        Log(faster, "개선 요약",
            $"시간 {beforeOpt.ms:F2} → {afterOpt.ms:F2} ms  /  {speedup:F1}x  /  메모리 절감 {FormatBytes(memorySaved)}");
    }

    // ══════════════════════════════════════════════════════════
    // TAB 1 — WreckEffectManager
    // ══════════════════════════════════════════════════════════
    private void DrawWreckTab()
    {
        GUILayout.Label("WreckEffectManager", EditorStyles.boldLabel);
        GUILayout.Label("벤치마크 프리팹들", EditorStyles.miniLabel);
        for (int i = 0; i < WreckBenchmarkPrefabSlots; i++)
        {
            _wreckBenchmarkPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                $"프리팹 {i + 1}",
                _wreckBenchmarkPrefabs[i],
                typeof(GameObject),
                false);
        }
        _wreckBenchmarkSeconds = EditorGUILayout.Slider("프레임 측정 시간", _wreckBenchmarkSeconds, 1f, 10f);
        _wreckBenchmarkRepeats = EditorGUILayout.IntSlider("반복 횟수", _wreckBenchmarkRepeats, 1, 100);

        bool canRun = Application.isPlaying && WreckEffectManager.Instance != null;
        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button("Budget Eviction 검증")) RunTest(TestWreckBudget);
            if (GUILayout.Button("Register / Unregister 검증")) RunTest(TestWreckRegisterUnregister);
            if (GUILayout.Button("Distance Culling 검증")) RunTest(TestWreckDistanceCulling);
            if (GUILayout.Button("Culling Before / After")) RunTest(TestWreckBeforeAfterComparison);
            if (GUILayout.Button("Optimization Matrix")) RunTest(StartWreckFrameBenchmark);
        }

        if (_wreckFrameBenchmarkRunning)
            EditorGUILayout.HelpBox(
                $"WreckFX 최적화 매트릭스 진행 중: {GetWreckBenchmarkModeLabel((WreckBenchmarkMode)_wreckBenchmarkModeIndex)}  ({_wreckBenchmarkCurrentRepeat + 1}/{_wreckBenchmarkRepeats})",
                MessageType.Info);

        if (!Application.isPlaying || WreckEffectManager.Instance == null) return;

        EditorGUILayout.Space(4);
        GUILayout.Label("── 현재 WreckFX 상태 ──", EditorStyles.miniLabel);
        DrawWreckLiveStats();
    }

    private void DrawWreckLiveStats()
    {
        var wm = WreckEffectManager.Instance;

        int count = 0;
        int max   = 0;
        float baseCull = 0;
        float currentCull = 0;

        if (FActiveFires?.GetValue(wm) is System.Collections.IList list) count = list.Count;
        if (FMaxFires?.GetValue(wm) is int m)   max  = m;
        if (FBaseCull?.GetValue(wm) is float bc) baseCull = bc;
        if (FCurrentCull?.GetValue(wm) is float cc) currentCull = cc;

        EditorGUILayout.LabelField("등록된 이펙트",   count.ToString());
        EditorGUILayout.LabelField("최대 허용 수",    max.ToString());
        EditorGUILayout.LabelField("기본 컬링 거리",  $"{baseCull} m");
        EditorGUILayout.LabelField("현재 컬링 거리",  $"{currentCull} m");

        var rect = EditorGUILayout.GetControlRect(false, 18);
        EditorGUI.ProgressBar(rect, max > 0 ? count / (float)max : 0f, $"{count} / {max}");
    }

    private void TestWreckBudget()
    {
        var wm = WreckEffectManager.Instance;
        if (FActiveFires?.GetValue(wm) is not System.Collections.IList list ||
            FMaxFires?.GetValue(wm) is not int max)
        { Log(false, "Budget Eviction", "리플렉션 실패 — 필드명 변경 확인 필요"); return; }

        int before = list.Count;

        // max + 3 더미 등록
        var dummies = new List<GameObject>(max + 3);
        for (int i = 0; i < max + 3; i++)
        {
            var go = new GameObject($"__WreckTest_{i}");
            go.AddComponent<ParticleSystem>();
            dummies.Add(go);
            wm.Register(go, go.transform);
        }

        int after = list.Count;
        bool evicted = after <= max;

        Log(evicted, "Budget Eviction",
            $"max={max}, {max + 3}개 등록 후 실제={after} — 초과분 강제 회수 {(evicted ? "확인 ✓" : "실패 ✗")}");

        // 정리
        foreach (var go in dummies)
        {
            wm.Unregister(go);
            DestroyImmediate(go);
        }
    }

    private void TestWreckRegisterUnregister()
    {
        var wm = WreckEffectManager.Instance;
        if (FActiveFires?.GetValue(wm) is not System.Collections.IList list)
        { Log(false, "Register/Unregister", "리플렉션 실패"); return; }

        int before = list.Count;

        var go = new GameObject("__WreckTest_RU");
        go.AddComponent<ParticleSystem>();

        wm.Register(go, go.transform);
        int afterReg = list.Count;

        wm.Unregister(go);
        int afterUnreg = list.Count;

        DestroyImmediate(go);

        Log(afterReg   == before + 1, "Register",   $"{before} → {afterReg} (+1 확인)");
        Log(afterUnreg == before,     "Unregister",  $"{afterReg} → {afterUnreg} (원복 확인)");
    }

    private void TestWreckDistanceCulling()
    {
        var wm = WreckEffectManager.Instance;
        var cam = CameraController.cameraInstance != null ? CameraController.cameraInstance.Cam : null;

        if (wm == null || cam == null)
        {
            Log(false, "Distance Culling", "WreckEffectManager 또는 카메라를 찾지 못함");
            return;
        }

        if (MEvaluateAll == null)
        {
            Log(false, "Distance Culling", "EvaluateAll 리플렉션 실패");
            return;
        }

        var go = new GameObject("__WreckTest_Culling");
        var ps = go.AddComponent<ParticleSystem>();
        var dir = cam.transform.forward.sqrMagnitude > 0.0001f ? cam.transform.forward.normalized : Vector3.forward;

        wm.Register(go, go.transform);

        try
        {
            MEvaluateAll.Invoke(wm, null);

            float currentCull = FCurrentCull?.GetValue(wm) is float cc
                ? cc
                : (FBaseCull?.GetValue(wm) is float bc ? bc : 0f);

            float farDistance = currentCull + Mathf.Max(5f, currentCull * 0.25f);
            float nearDistance = Mathf.Max(1f, currentCull * 0.5f);

            go.transform.position = cam.transform.position + dir * farDistance;
            MEvaluateAll.Invoke(wm, null);
            bool farOff = !ps.emission.enabled;

            go.transform.position = cam.transform.position + dir * nearDistance;
            MEvaluateAll.Invoke(wm, null);
            bool nearOn = ps.emission.enabled;

            Log(farOff, "Distance Culling (Far)",
                $"컷오프 {currentCull:F1}m / 테스트 {farDistance:F1}m → emission {(farOff ? "OFF ✓" : "ON ✗")}");
            Log(nearOn, "Distance Culling (Near)",
                $"컷오프 {currentCull:F1}m / 테스트 {nearDistance:F1}m → emission {(nearOn ? "ON ✓" : "OFF ✗")}");
            Log(farOff && nearOn, "Distance Culling",
                $"원거리 off / 근거리 on 전환 {(farOff && nearOn ? "확인 ✓" : "실패 ✗")}");
        }
        finally
        {
            wm.Unregister(go);
            DestroyImmediate(go);
        }
    }

    private void TestWreckBeforeAfterComparison()
    {
        var wm = WreckEffectManager.Instance;
        var cam = CameraController.cameraInstance != null ? CameraController.cameraInstance.Cam : null;

        if (wm == null || cam == null || MEvaluateAll == null)
        {
            Log(false, "Culling Before / After", "WreckEffectManager, 카메라 또는 EvaluateAll 접근 실패");
            return;
        }

        float currentCull = FCurrentCull?.GetValue(wm) is float cc
            ? cc
            : (FBaseCull?.GetValue(wm) is float bc ? bc : 100f);

        int activeCount = FActiveFires?.GetValue(wm) is System.Collections.IList activeList ? activeList.Count : 0;
        int maxFires = FMaxFires?.GetValue(wm) is int max ? max : 0;
        int availableSlots = maxFires - activeCount;
        if (availableSlots <= 0)
        {
            Log(false, "Culling Before / After", $"비교용 여유 슬롯이 없음 (활성 {activeCount} / 최대 {maxFires})");
            return;
        }

        int sampleCount = Mathf.Clamp(Mathf.Min(_spawnN, availableSlots), 1, 100);
        float farDistance = currentCull + Mathf.Max(5f, currentCull * 0.25f);
        var dir = cam.transform.forward.sqrMagnitude > 0.0001f ? cam.transform.forward.normalized : Vector3.forward;
        var dummies = new List<GameObject>(sampleCount);

        try
        {
            for (int i = 0; i < sampleCount; i++)
            {
                var go = new GameObject($"__WreckCullCompare_{i}");
                go.AddComponent<ParticleSystem>();
                go.transform.position = cam.transform.position + dir * farDistance + Vector3.right * (i * 0.1f);
                dummies.Add(go);
                wm.Register(go, go.transform);
            }

            int beforeCount = CountEmissionEnabled(dummies);
            PerfSample afterOpt = MeasurePerf(() => MEvaluateAll.Invoke(wm, null));
            int afterCount = CountEmissionEnabled(dummies);

            Log(true, "Before (비최적화 가정)",
                $"원거리 VFX {sampleCount}개를 예산 내 등록  /  emission ON {beforeCount}개");
            Log(afterCount < beforeCount, "After (컬링 적용)",
                $"컷오프 {currentCull:F1}m 밖 {farDistance:F1}m 배치 후 emission ON {beforeCount} → {afterCount}");
            Log(true, "Culling 비용",
                $"EvaluateAll 1회 {afterOpt.ms:F2} ms  /  managed Δ {FormatBytes(afterOpt.managedBytes)}");
        }
        finally
        {
            foreach (var go in dummies)
            {
                if (go == null) continue;
                wm.Unregister(go);
                DestroyImmediate(go);
            }
        }
    }

    private void StartWreckFrameBenchmark()
    {
        var wm = WreckEffectManager.Instance;
        var cam = CameraController.cameraInstance != null ? CameraController.cameraInstance.Cam : null;
        if (wm == null || cam == null || MEvaluateAll == null)
        {
            Log(false, "Wreck Frame Benchmark", "WreckEffectManager, 카메라 또는 EvaluateAll 접근 실패");
            return;
        }

        CleanupWreckBenchmarkObjects();
        _results.Clear();

        // If a previous benchmark left temporary values behind, restore before recapturing the baseline.
        if (_wreckBenchmarkBaseCullOriginal > 0f)
            RestoreWreckBenchmarkOriginals(wm);

        _wreckBenchmarkBaseCullOriginal = FBaseCull?.GetValue(wm) is float baseCull ? baseCull : 100f;
        _wreckBenchmarkMaxOriginal = FMaxFires?.GetValue(wm) is int max ? max : 0;
        for (int i = 0; i < _wreckBenchmarkSamples.Length; i++)
        {
            _wreckBenchmarkSamples[i] = default;
            _wreckBenchmarkRuns[i].Clear();
        }

        _wreckFrameBenchmarkRunning = true;
        _wreckBenchmarkCurrentRepeat = 0;
        _wreckBenchmarkModeIndex = 0;
        SetupWreckBenchmarkMode(wm, cam, (WreckBenchmarkMode)_wreckBenchmarkModeIndex);
        Repaint();
    }

    // ══════════════════════════════════════════════════════════
    // TAB 2 — 전체 실행
    // ══════════════════════════════════════════════════════════
    private void DrawAllTab()
    {
        GUILayout.Label("전체 테스트 실행", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Pool 테스트는 '테스트 프리팹'이 지정돼 있어야 합니다.\n" +
            "WreckFX / 음속 테스트는 프리팹 없이도 실행됩니다.", MessageType.None);

        _spawnPrefab = (GameObject)EditorGUILayout.ObjectField("Pool 테스트 프리팹", _spawnPrefab, typeof(GameObject), false);
        _spawnN      = EditorGUILayout.IntSlider("스폰 수", _spawnN, 10, 500);

        EditorGUILayout.Space(4);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("▶  전체 실행", GUILayout.Height(36)))
                RunTest(RunAll);
        }
    }

    private void RunAll()
    {
        // WreckFX
        if (Application.isPlaying && WreckEffectManager.Instance != null)
        {
            TestWreckRegisterUnregister();
            TestWreckBudget();
            TestWreckDistanceCulling();
            TestWreckBeforeAfterComparison();
        }

        // Pool
        if (Application.isPlaying && _spawnPrefab != null && PoolManager.Instance != null)
        {
            TestPoolBeforeAfterComparison();
            TestPoolSnapshot();
            TestPoolSpawnReturn();
            TestPoolVsInstantiate();
        }
    }

    // ══════════════════════════════════════════════════════════
    // 공통 헬퍼
    // ══════════════════════════════════════════════════════════
    private void RunTest(System.Action test)
    {
        _results.Clear();
        test();
        Repaint();
    }

    private void SampleWreckBenchmarkFrame()
    {
        _wreckFrameCount++;
        _wreckFrameTotalDt += Time.unscaledDeltaTime;
        _wreckFrameMinDt = Mathf.Min(_wreckFrameMinDt, Time.unscaledDeltaTime);
        _wreckFrameMaxDt = Mathf.Max(_wreckFrameMaxDt, Time.unscaledDeltaTime);

        if (EditorApplication.timeSinceStartup < _wreckFrameBenchmarkEndsAt)
            return;

        var wm = WreckEffectManager.Instance;
        if (wm == null)
        {
            StopWreckFrameBenchmark("WreckEffectManager가 사라져 벤치마크 중단");
            return;
        }

        FrameSample sample = EndFrameSampling();
        _wreckBenchmarkSamples[_wreckBenchmarkModeIndex] = sample;
        _wreckBenchmarkRuns[_wreckBenchmarkModeIndex].Add(sample);
        _wreckBenchmarkModeIndex++;

        var cam = CameraController.cameraInstance != null ? CameraController.cameraInstance.Cam : null;
        if (_wreckBenchmarkModeIndex < _wreckBenchmarkSamples.Length && cam != null)
        {
            SetupWreckBenchmarkMode(wm, cam, (WreckBenchmarkMode)_wreckBenchmarkModeIndex);
            Repaint();
            return;
        }

        _wreckBenchmarkCurrentRepeat++;
        if (_wreckBenchmarkCurrentRepeat < _wreckBenchmarkRepeats && cam != null)
        {
            _wreckBenchmarkModeIndex = 0;
            SetupWreckBenchmarkMode(wm, cam, (WreckBenchmarkMode)_wreckBenchmarkModeIndex);
            Repaint();
            return;
        }

        RestoreWreckBenchmarkOriginals(wm);
        LogWreckBenchmarkMatrix();
        StopWreckFrameBenchmark(null);
    }

    private void BeginFrameSampling()
    {
        _wreckFrameMemoryStart = CaptureManagedMemory();
        _wreckFrameTotalDt = 0f;
        _wreckFrameMinDt = float.MaxValue;
        _wreckFrameMaxDt = 0f;
        _wreckFrameCount = 0;
        _wreckFrameBenchmarkEndsAt = EditorApplication.timeSinceStartup + _wreckBenchmarkSeconds;
    }

    private FrameSample EndFrameSampling()
    {
        long memoryEnd = CaptureManagedMemory();
        float averageDt = _wreckFrameCount > 0 ? _wreckFrameTotalDt / _wreckFrameCount : 0f;
        float averageMs = averageDt * 1000f;
        return new FrameSample
        {
            averageMs = averageMs,
            minMs = _wreckFrameMinDt == float.MaxValue ? 0f : _wreckFrameMinDt * 1000f,
            maxMs = _wreckFrameMaxDt * 1000f,
            averageFps = averageDt > 0.00001f ? 1f / averageDt : 0f,
            frames = _wreckFrameCount,
            managedDelta = memoryEnd - _wreckFrameMemoryStart
        };
    }

    private void StopWreckFrameBenchmark(string reason)
    {
        _wreckFrameBenchmarkRunning = false;
        _wreckBenchmarkCurrentRepeat = 0;
        _wreckBenchmarkModeIndex = 0;
        RestoreWreckBenchmarkOriginals(WreckEffectManager.Instance);
        CleanupWreckBenchmarkObjects();
        if (!string.IsNullOrEmpty(reason))
            Log(false, "Wreck Frame Benchmark", reason);
        Repaint();
    }

    private void SetupWreckBenchmarkMode(WreckEffectManager wm, Camera cam, WreckBenchmarkMode mode)
    {
        CleanupWreckBenchmarkObjects();
        ApplyWreckOptimizationMode(wm, mode, restoreOriginals: false);
        SpawnWreckBenchmarkObjects(wm, cam, Mathf.Max(1, _spawnN));
        BeginFrameSampling();
    }

    private void ApplyWreckOptimizationMode(WreckEffectManager wm, WreckBenchmarkMode mode, bool restoreOriginals)
    {
        int highMax = Mathf.Max(_wreckBenchmarkMaxOriginal, Mathf.Max(1, _spawnN));
        float hugeCull = 100000f;

        if (restoreOriginals)
        {
            FMaxFires?.SetValue(wm, _wreckBenchmarkMaxOriginal);
            ApplyWreckCullDistance(wm, _wreckBenchmarkBaseCullOriginal);
            return;
        }

        switch (mode)
        {
            case WreckBenchmarkMode.None:
                FMaxFires?.SetValue(wm, highMax);
                ApplyWreckCullDistance(wm, hugeCull);
                break;
            case WreckBenchmarkMode.BudgetOnly:
                FMaxFires?.SetValue(wm, _wreckBenchmarkMaxOriginal);
                ApplyWreckCullDistance(wm, hugeCull);
                break;
            case WreckBenchmarkMode.DistanceOnly:
                FMaxFires?.SetValue(wm, highMax);
                ApplyWreckCullDistance(wm, _wreckBenchmarkBaseCullOriginal);
                break;
            case WreckBenchmarkMode.Both:
                FMaxFires?.SetValue(wm, _wreckBenchmarkMaxOriginal);
                ApplyWreckCullDistance(wm, _wreckBenchmarkBaseCullOriginal);
                break;
        }
    }

    private void RestoreWreckBenchmarkOriginals(WreckEffectManager wm)
    {
        if (wm == null || _wreckBenchmarkBaseCullOriginal <= 0f)
            return;

        ApplyWreckOptimizationMode(wm, WreckBenchmarkMode.Both, restoreOriginals: true);
    }

    private void LogWreckBenchmarkMatrix()
    {
        FrameAggregate none = AggregateRuns(_wreckBenchmarkRuns[(int)WreckBenchmarkMode.None]);
        FrameAggregate budgetOnly = AggregateRuns(_wreckBenchmarkRuns[(int)WreckBenchmarkMode.BudgetOnly]);
        FrameAggregate distanceOnly = AggregateRuns(_wreckBenchmarkRuns[(int)WreckBenchmarkMode.DistanceOnly]);
        FrameAggregate both = AggregateRuns(_wreckBenchmarkRuns[(int)WreckBenchmarkMode.Both]);

        Log(true, "None",
            $"avg {none.averageMs:F2} ms / {none.averageFps:F1} FPS / min-max {none.minMs:F2}-{none.maxMs:F2} ms / {none.runs} runs");
        Log(budgetOnly.averageMs <= none.averageMs, "Budget Only",
            $"avg {budgetOnly.averageMs:F2} ms / {budgetOnly.averageFps:F1} FPS / delta {PercentDelta(none.averageMs, budgetOnly.averageMs)} / min-max {budgetOnly.minMs:F2}-{budgetOnly.maxMs:F2}");
        Log(distanceOnly.averageMs <= none.averageMs, "Distance Only",
            $"avg {distanceOnly.averageMs:F2} ms / {distanceOnly.averageFps:F1} FPS / delta {PercentDelta(none.averageMs, distanceOnly.averageMs)} / min-max {distanceOnly.minMs:F2}-{distanceOnly.maxMs:F2}");
        Log(both.averageMs <= none.averageMs, "Both",
            $"avg {both.averageMs:F2} ms / {both.averageFps:F1} FPS / delta {PercentDelta(none.averageMs, both.averageMs)} / min-max {both.minMs:F2}-{both.maxMs:F2}");

        float bestMs = Mathf.Min(none.averageMs, budgetOnly.averageMs, distanceOnly.averageMs, both.averageMs);
        string bestLabel = GetWreckBenchmarkModeLabel(FindBestWreckBenchmarkMode(bestMs));
        Log(true, "Best Mode",
            $"{bestLabel}  /  best avg {bestMs:F2} ms  /  baseline 대비 {PercentDelta(none.averageMs, bestMs)}");
    }

    private WreckBenchmarkMode FindBestWreckBenchmarkMode(float bestMs)
    {
        for (int i = 0; i < _wreckBenchmarkRuns.Length; i++)
        {
            FrameAggregate agg = AggregateRuns(_wreckBenchmarkRuns[i]);
            if (Mathf.Approximately(agg.averageMs, bestMs))
                return (WreckBenchmarkMode)i;
        }
        return WreckBenchmarkMode.None;
    }

    private static FrameAggregate AggregateRuns(List<FrameSample> runs)
    {
        if (runs == null || runs.Count == 0)
            return default;

        float sumMs = 0f;
        float sumFps = 0f;
        long sumManaged = 0;
        float minMs = float.MaxValue;
        float maxMs = 0f;

        for (int i = 0; i < runs.Count; i++)
        {
            FrameSample run = runs[i];
            sumMs += run.averageMs;
            sumFps += run.averageFps;
            sumManaged += run.managedDelta;
            minMs = Mathf.Min(minMs, run.averageMs);
            maxMs = Mathf.Max(maxMs, run.averageMs);
        }

        return new FrameAggregate
        {
            averageMs = sumMs / runs.Count,
            averageFps = sumFps / runs.Count,
            minMs = minMs,
            maxMs = maxMs,
            averageManagedDelta = sumManaged / runs.Count,
            runs = runs.Count
        };
    }

    private static string GetWreckBenchmarkModeLabel(WreckBenchmarkMode mode)
    {
        switch (mode)
        {
            case WreckBenchmarkMode.None: return "None";
            case WreckBenchmarkMode.BudgetOnly: return "Budget Only";
            case WreckBenchmarkMode.DistanceOnly: return "Distance Only";
            case WreckBenchmarkMode.Both: return "Both";
            default: return mode.ToString();
        }
    }

    private void SpawnWreckBenchmarkObjects(WreckEffectManager wm, Camera cam, int count)
    {
        float currentCull = FCurrentCull?.GetValue(wm) is float cc
            ? cc
            : (FBaseCull?.GetValue(wm) is float bc ? bc : 100f);
        float farDistance = currentCull + Mathf.Max(5f, currentCull * 0.5f);
        Vector3 dir = cam.transform.forward.sqrMagnitude > 0.0001f ? cam.transform.forward.normalized : Vector3.forward;
        List<GameObject> prefabs = GetAssignedWreckBenchmarkPrefabs();

        for (int i = 0; i < count; i++)
        {
            GameObject sourcePrefab = prefabs.Count > 0 ? prefabs[i % prefabs.Count] : null;
            GameObject go = sourcePrefab != null
                ? Instantiate(sourcePrefab)
                : new GameObject($"__WreckFrameBench_{i}");

            if (go.GetComponentInChildren<ParticleSystem>(true) == null)
                go.AddComponent<ParticleSystem>();

            go.name = $"__WreckFrameBench_{i}";
            go.transform.position = cam.transform.position + dir * farDistance + Vector3.right * (i * 0.5f);
            _wreckBenchmarkObjects.Add(go);
            wm.Register(go, go.transform);
        }
    }

    private List<GameObject> GetAssignedWreckBenchmarkPrefabs()
    {
        var prefabs = new List<GameObject>(WreckBenchmarkPrefabSlots);
        for (int i = 0; i < _wreckBenchmarkPrefabs.Length; i++)
        {
            if (_wreckBenchmarkPrefabs[i] != null)
                prefabs.Add(_wreckBenchmarkPrefabs[i]);
        }
        return prefabs;
    }

    private void CleanupWreckBenchmarkObjects()
    {
        var wm = WreckEffectManager.Instance;
        foreach (var go in _wreckBenchmarkObjects)
        {
            if (go == null) continue;
            wm?.Unregister(go);
            DestroyImmediate(go);
        }
        _wreckBenchmarkObjects.Clear();
    }

    private static void ApplyWreckCullDistance(WreckEffectManager wm, float baseCullDistance)
    {
        FBaseCull?.SetValue(wm, baseCullDistance);
        FCurrentCull?.SetValue(wm, baseCullDistance);
        MEvaluateAll?.Invoke(wm, null);
    }

    private static PerfSample MeasurePerf(System.Action action)
    {
        long before = CaptureManagedMemory();
        var sw = SW.StartNew();
        action();
        sw.Stop();
        long after = CaptureManagedMemory();

        return new PerfSample
        {
            ms = sw.Elapsed.TotalMilliseconds,
            managedBytes = after - before
        };
    }

    private static long CaptureManagedMemory()
    {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        return Profiler.GetMonoUsedSizeLong();
    }

    private static int CountEmissionEnabled(List<GameObject> objects)
    {
        int count = 0;
        foreach (var go in objects)
        {
            if (go == null) continue;
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null && ps.emission.enabled)
                count++;
        }
        return count;
    }

    private static string FormatBytes(long bytes)
    {
        string sign = bytes > 0 ? "+" : bytes < 0 ? "-" : "";
        long abs = System.Math.Abs(bytes);

        if (abs >= 1024 * 1024)
            return $"{sign}{abs / (1024f * 1024f):F2} MB";
        if (abs >= 1024)
            return $"{sign}{abs / 1024f:F1} KB";
        return $"{sign}{abs} B";
    }

    private static string PercentDelta(float baselineMs, float currentMs)
    {
        if (baselineMs <= 0.0001f)
            return "n/a";

        float pct = ((baselineMs - currentMs) / baselineMs) * 100f;
        return $"{pct:+0.0;-0.0;0.0}%";
    }

    private void Log(bool pass, string label, string detail) =>
        _results.Add(new TestResult { pass = pass, label = label, detail = detail });

    private void DrawResults()
    {
        if (_results.Count == 0) return;

        EditorGUILayout.Space(2);
        GUILayout.Label("── 결과 ──", EditorStyles.miniLabel);

        int passCount = 0, failCount = 0;
        var stylePass = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.25f, 0.75f, 0.25f) } };
        var styleFail = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.90f, 0.30f, 0.30f) } };

        foreach (var r in _results)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(r.pass ? "✓" : "✗", r.pass ? stylePass : styleFail, GUILayout.Width(16));
            GUILayout.Label(r.label,  GUILayout.Width(180));
            GUILayout.Label(r.detail, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (r.pass) passCount++; else failCount++;
        }

        EditorGUILayout.Space(2);
        var summary = $"통과: {passCount}  /  실패: {failCount}";
        var styleSum = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = failCount == 0 ? new Color(0.25f, 0.75f, 0.25f) : new Color(0.90f, 0.30f, 0.30f) }
        };
        GUILayout.Label(summary, styleSum);

        if (GUILayout.Button("초기화", GUILayout.Width(60))) { _results.Clear(); Repaint(); }
    }
}
