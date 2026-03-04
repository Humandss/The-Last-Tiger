using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum AmmoType { None, AP, HE }

public interface ITankLoader
{
    void LoadDefault();
    void Load(AmmoType type);
    bool GetIsLoading();
    bool GetIsLoaded();
    void IsShot();
    ShellData GetLoadedShell();

    AmmoType GetLoadedAmmoType();
}
public class LoaderController : MonoBehaviour, ITankLoader
{
    [Header("Loader")]
    [SerializeField] private float reloadTime = 10.0f;
    [SerializeField] private bool loaderDead;
    [SerializeField, Range(0f, 2f)] private float reloadTimeMul = 1f; //
    [SerializeField] private float reloadTimeMulSmoothed = 1f;
    [SerializeField] private float loaderMulSmooth = 10f;

    [Header("Defaults")]
    [SerializeField] private AmmoType defaultAmmo = AmmoType.AP;
    private AmmoType shellType = AmmoType.None;

    [Header("Shell Data Table")]
    [SerializeField] private ShellData apShell;
    [SerializeField] private ShellData heShell;
    private ShellData loadedShell; // 현재 장전된 ShellData

    private bool isLoading;
    private bool isLoaded;
    private Coroutine co;
    private AmmoType LastSelectedAmmo;
    private float loading01;

    void Awake()
    {
        LastSelectedAmmo = defaultAmmo;
        isLoaded = false;
        isLoading = false;
    }

    private void Update()
    {
        // 로더 배율 스무딩
        float a = 1f - Mathf.Exp(-loaderMulSmooth * Time.deltaTime);
        reloadTimeMulSmoothed = Mathf.Lerp(reloadTimeMulSmoothed, reloadTimeMul, a);

        // 장전 중인데 로더 죽으면 즉시 중단
        if (loaderDead && isLoading)
            CeaseAction();

        if (Input.GetKeyDown(KeyCode.R)) Load(LastSelectedAmmo);
        if (Input.GetKeyDown(KeyCode.Alpha1)) Load(AmmoType.AP);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Load(AmmoType.HE);

    }
    public void LoadDefault()
    {
        Load(LastSelectedAmmo);
    }

    public void Load(AmmoType type)
    {
        if (type == AmmoType.None) return;

        if (loaderDead)
        {
            Debug.Log("[Loader] 로더 사망 -> 장전 불가");
            return;
        }


        LastSelectedAmmo = type;

        if (isLoaded)
        {
            Debug.Log($"[Loader] 이미 {shellType} 장전됨. 교체 불가!");
            return;
        }
     
        // 장전 중이면 취소 후 재시작
        CeaseAction();
        isLoaded = false;
        co = StartCoroutine(LoadRoutine(type));
        loadedShell = GetShellDataFor(LastSelectedAmmo);
    }

    private void CeaseAction()
    {
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }

        isLoading = false;
        loading01 = 0.0f;

        Debug.Log("[Loader] CeaseAction (장전 취소)");
    }

    private IEnumerator LoadRoutine(AmmoType type)
    {
        isLoading = true;
        isLoaded = false;

        loading01 = 0.0f;

        float t = 0.0f;
        float dur = reloadTime * reloadTimeMulSmoothed;

        Debug.Log($"[Loader] {type} 장전 시작 ({dur:0.0}s)");

        while (t < dur)
        {
            if (loaderDead)
            {
                Debug.Log("[Loader] 장전 중 로더 사망 -> 장전 중단");
                isLoading = false;
                loading01 = 0.0f;
                co = null;
                yield break;
            }

            t += Time.deltaTime;
            loading01 = Mathf.Clamp01(t / dur);
            yield return null;
        }

        isLoading = false;
        loading01 = 1.0f;
        shellType = type;
      
        isLoaded = true;

        Debug.Log($"[Loader] {shellType} 장전 완료");
        co = null;
    }
    public ShellData GetLoadedShell()
    {
        return loadedShell;
    }
    public AmmoType GetLoadedAmmoType()
    {
        return shellType;
    }
    public bool GetIsLoading()
    {
        return isLoading;
    }
    public bool GetIsLoaded()
    {
        return isLoaded;
    }
    public void IsShot()
    {
        isLoaded = false;
        shellType = AmmoType.None;
        loadedShell = null;
    }

    private ShellData GetShellDataFor(AmmoType type)
    {
        switch (type)
        {
            case AmmoType.AP: return apShell;
            case AmmoType.HE: return heShell;
            default: return null;
        }
    }

    public void SetLoaderState(bool dead, float hpRatio)
    {
        loaderDead = dead;

        hpRatio = Mathf.Clamp01(hpRatio);
        reloadTimeMul = Mathf.Lerp(2f, 1f, hpRatio);
    }
}
