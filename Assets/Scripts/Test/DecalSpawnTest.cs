using UnityEngine;

/// <summary>
/// 테스트용: 마우스 클릭한 지점에 Decal Projector 프리팹을 스폰.
/// PoolManager 연동 확인 + 향후 BallisticManager 연동 전 검증.
/// </summary>
public class DecalSpawnTest : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject decalPrefab;

    [Header("Spawn")]
    [SerializeField] private KeyCode spawnKey = KeyCode.Mouse0;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float maxDistance = 500f;
    [SerializeField] private float lifetime = 30f;

    [Header("Offset")]
    [Tooltip("데칼을 표면에서 살짝 띄워 z-fighting 방지")]
    [SerializeField] private float surfaceOffset = 0.05f;

    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(spawnKey)) return;
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null || decalPrefab == null) return;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            Debug.Log("[DecalTest] No surface hit.");
            return;
        }

        SpawnDecal(hit.point, hit.normal);
    }

    private void SpawnDecal(Vector3 point, Vector3 normal)
    {
        Vector3 spawnPos = point + normal * surfaceOffset;

        // DecalProjector는 +Z 방향으로 투영 → 표면 법선 반대방향을 forward로
        Vector3 projectDir = -normal;
        Quaternion rot = Quaternion.LookRotation(projectDir, Vector3.up);

        // 랜덤 롤(z축 회전)로 같은 데칼 반복 시 어색함 방지
        rot *= Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        GameObject decal = PoolManager.Instance.Spawn(decalPrefab, spawnPos, rot);
        if (decal == null)
        {
            Debug.LogWarning("[DecalTest] Spawn failed.");
            return;
        }

        PoolManager.Instance.ReturnDelayed(decal, lifetime);
        Debug.Log($"[DecalTest] Spawned decal at {point}");
    }
}
