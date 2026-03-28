using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    // ── GameObject Pool ─────────────────────────────────────

    [SerializeField] private bool isExpandable = true;

    private readonly Dictionary<GameObject, Queue<GameObject>> prefabToPool
        = new Dictionary<GameObject, Queue<GameObject>>();

    private readonly Dictionary<GameObject, GameObject> instanceToPrefab
        = new Dictionary<GameObject, GameObject>();

    // ── Sound Pool ──────────────────────────────────────────

    [Header("Sound Pool")]
    [SerializeField] private int soundPoolInitialSize = 16;
    [SerializeField] private int soundPoolMaxSize = 32;

    private readonly Queue<AudioSource> soundPool = new Queue<AudioSource>();
    private readonly Queue<AudioSource> activeSounds = new Queue<AudioSource>();
    private readonly Dictionary<AudioSource, Coroutine> soundCoroutines = new Dictionary<AudioSource, Coroutine>();

    // ── 초기화 ──────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < soundPoolInitialSize; i++)
            soundPool.Enqueue(CreateAudioSource());
    }

    // ── Sound Pool API ──────────────────────────────────────

    public void PlaySound(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetAudioSource();
        if (source == null) return;

        source.transform.position = position;
        source.clip = clip;
        source.volume = volume;
        source.gameObject.SetActive(true);
        source.Play();
        activeSounds.Enqueue(source);

        Coroutine coroutine = StartCoroutine(CoReturnSound(source, clip.length));
        soundCoroutines[source] = coroutine;
    }

    private AudioSource GetAudioSource()
    {
        if (soundPool.Count > 0)
            return soundPool.Dequeue();

        if (activeSounds.Count < soundPoolMaxSize)
            return CreateAudioSource();

        // 버젯 초과 시 가장 오래된 활성 소스를 강제 회수
        AudioSource oldest = activeSounds.Dequeue();
        if (soundCoroutines.TryGetValue(oldest, out var coroutine))
        {
            StopCoroutine(coroutine);
            soundCoroutines.Remove(oldest);
        }
        oldest.Stop();
        oldest.clip = null;
        return oldest;
    }

    private AudioSource CreateAudioSource()
    {
        GameObject obj = new GameObject("PooledAudioSource");
        obj.transform.SetParent(transform);
        AudioSource source = obj.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        obj.SetActive(false);
        return source;
    }

    private IEnumerator CoReturnSound(AudioSource source, float duration)
    {
        yield return new WaitForSeconds(duration);
        source.Stop();
        source.clip = null;
        source.gameObject.SetActive(false);
        soundCoroutines.Remove(source);
        soundPool.Enqueue(source);
    }

    // ── GameObject Pool API ─────────────────────────────────

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError($"[PoolManager.Spawn] prefab is NULL");
            return null;
        }

        if (!prefabToPool.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<GameObject>();
            prefabToPool.Add(prefab, queue);
        }

        GameObject obj;
        if (queue.Count > 0)
        {
            obj = queue.Dequeue();
        }
        else
        {
            if (!isExpandable)
            {
                Debug.LogWarning($"[PoolManager.Spawn] {prefab.name} pool is not expandable.");
                return null;
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
            Debug.LogWarning("[PoolManager.Return] Unknown instance, destroying.");
            Destroy(instance);
            return;
        }

        if (!prefabToPool.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<GameObject>();
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

    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab);
        instanceToPrefab[obj] = prefab;
        return obj;
    }

    private IEnumerator CoReturnDelayed(GameObject instance, float delay)
    {
        yield return new WaitForSeconds(delay);
        Return(instance);
    }
}
