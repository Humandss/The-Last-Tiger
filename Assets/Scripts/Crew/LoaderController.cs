using System.Collections;
using UnityEditor.Experimental.GraphView;
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
    [Header("Refs")]
    [SerializeField] private bool isAI = false;
    [SerializeField] private PlayerTankSoundController soundController;

    [Header("Loader")]
    [SerializeField] private float reloadTime = 10.0f;
    [SerializeField] private bool loaderDead;
    [SerializeField] private bool loaderTaskBlocked;
    [SerializeField, Range(0f, 2f)] private float reloadTimeMul = 1f;
    [SerializeField] private float reloadTimeMulSmoothed = 1f;
    [SerializeField] private float loaderMulSmooth = 10f;
    [SerializeField, Range(0f, 1f)] private float loaderHpRatio = 1f;

    [Header("Defaults")]
    [SerializeField] private AmmoType defaultAmmo = AmmoType.AP;
    private AmmoType shellType = AmmoType.None;

    [Header("Shell Data Table")]
    [SerializeField] private ShellData apShell;
    [SerializeField] private ShellData heShell;
    private ShellData loadedShell;

    private bool isLoading;
    private bool isLoaded;
    private Coroutine co;
    private AmmoType LastSelectedAmmo;
    private float loading01;

    void Awake()
    {
        if (!isAI) soundController = GetComponent<PlayerTankSoundController>();
        LastSelectedAmmo = defaultAmmo;
        isLoaded = false;
        isLoading = false;
    }

    private void Update()
    {
        float a = 1f - Mathf.Exp(-loaderMulSmooth * Time.deltaTime);
        reloadTimeMulSmoothed = Mathf.Lerp(reloadTimeMulSmoothed, reloadTimeMul, a);

        if (isAI) return;

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

        LastSelectedAmmo = type;

        if (isLoaded)
        {
            Debug.Log($"[Loader] Already loaded with {shellType}, cannot swap.");
            return;
        }

        CeaseAction();
        isLoaded = false;
        co = StartCoroutine(LoadRoutine(type));
        loadedShell = GetShellDataFor(LastSelectedAmmo);

        if (!isAI && soundController != null)
        {
            soundController.PlayReload();
            if (type == AmmoType.AP) soundController.PlayAPLoad();
            else if (type == AmmoType.HE) soundController.PlayHELoad();
            else return;
             
        }
            
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

        Debug.Log("[Loader] CeaseAction (reload canceled)");
    }

    private IEnumerator LoadRoutine(AmmoType type)
    {
        isLoading = true;
        isLoaded = false;
        loading01 = 0.0f;

        float initialDuration = reloadTime * reloadTimeMulSmoothed;

        Debug.Log($"[Loader] Reload start {type} ({initialDuration:0.0}s)");

        while (loading01 < 1f)
        {
            float currentDuration = Mathf.Max(0.01f, reloadTime * reloadTimeMulSmoothed);
            loading01 = Mathf.Clamp01(loading01 + (Time.deltaTime / currentDuration));
            yield return null;
        }

        isLoading = false;
        loading01 = 1.0f;
        shellType = type;
        isLoaded = true;

        Debug.Log($"[Loader] Reload complete {shellType}");
        if(!isAI) soundController.PlayReloadCrewVoice();
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

    public bool IsAI => isAI;

    public float GetLoading01()
    {
        return loading01;
    }

    public float GetReloadSecondsRemaining()
    {
        if (!isLoading) return 0f;
        float duration = Mathf.Max(0.01f, reloadTime * reloadTimeMulSmoothed);
        return Mathf.Max(0f, duration * (1f - loading01));
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
        loaderHpRatio = Mathf.Clamp01(hpRatio);
        RecalculateReloadMultiplier();
    }

    public void SetLoaderTaskBlocked(bool blocked)
    {
        loaderTaskBlocked = blocked;
        RecalculateReloadMultiplier();
    }

    private void RecalculateReloadMultiplier()
    {
        if (loaderDead || loaderTaskBlocked)
        {
            reloadTimeMul = 2f;
            return;
        }

        reloadTimeMul = Mathf.Lerp(2f, 1f, loaderHpRatio);
    }
}
