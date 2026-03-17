using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [SerializeField] private bool isExpandable = true;

    //���� ������Ʈ �� ť ��ųʸ�
    private readonly Dictionary<GameObject, Queue<GameObject>> prefabToPool 
        = new Dictionary<GameObject, Queue<GameObject>>();

    //������Ʈ �� �ν��Ͻ� ��ųʸ�
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab 
        = new Dictionary<GameObject, GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab);
        instanceToPrefab[obj] = prefab;
        return obj;
    }
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError($"[PoolManager.Spawn] {prefab} is NULL");
            return null;
        }
        //if new prefab use Spawn func, add to dic
        if(!prefabToPool.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<GameObject>();
            prefabToPool.Add(prefab, queue);
        }

        GameObject obj;
        if (queue.Count > 0) obj = queue.Dequeue();
        else
        {
            if (!isExpandable)
            {
                Debug.LogWarning($"[PoolManager.Spawn] {prefab.name} is not expandable.");
                return null ;
            }
            obj = CreateInstance(prefab);

        }

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        return obj;
    }

    public void Return(GameObject instance)
    {
        if (instance == null) return;

        if (!instanceToPrefab.TryGetValue(instance, out var prefab))
        {
            Debug.LogWarning("[PoolManager.Return]: unknown instance, Destroy");
            Destroy(instance);
            return;
        }

        if (!prefabToPool.TryGetValue(prefab, out var queue))
        {
            queue= new Queue<GameObject>();
            prefabToPool.Add(prefab, queue);
        }

        instance.SetActive(false);
        queue.Enqueue(instance);
    }

    public void ReturnDelayed(GameObject instance, float delay)
    {
        if (instance == null) return;
        StartCoroutine(CoReturnDelayed(instance, delay));
    }

    private IEnumerator CoReturnDelayed(GameObject instance, float delay)
    {
        yield return new WaitForSeconds(delay);
        Return(instance);
    }

}
