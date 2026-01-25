using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum AmmoType { None, AP, HE }

public interface ITankLoader
{
    void LoadDefault();
    void Load(AmmoType type);
    bool GetIsReloading();
    bool GetIsReloaded();

}
public class LoaderController : MonoBehaviour, ITankLoader
{
    [Header("Load Times (sec)")]
    private float reloadTime = 10.0f;
    [SerializeField] private float timeMul = 1.0f;

    [Header("Defaults")]
    [SerializeField] private AmmoType defaultAmmo = AmmoType.AP;
    private AmmoType shellType = AmmoType.None;

    private bool isLoading;
    private bool isLoaded;
    private Coroutine co;
    private AmmoType LastSelectedAmmo;
    private float loading01;

    void Awake()
    {
        LastSelectedAmmo = defaultAmmo;
    }

    public void LoadDefault()
    {
        Load(LastSelectedAmmo);
    }

    public void Load(AmmoType type)
    {
        LastSelectedAmmo = type;

        if (type == AmmoType.None) return;

        if (isLoaded)
        {
            Debug.Log($"[Loader] 이미 {shellType} 장전됨. 교체 불가!");
            return;
        }
     
        // 장전 중이면 취소 후 재시작
        CeaseAction();

        isLoaded = false;
        co = StartCoroutine(LoadRoutine(type));
    }

    private void CeaseAction()
    {
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }

        isLoading = false;
        loading01 = 0f;

        // 장전 완료 탄까지 날릴지(취소 시 탄 제거) 정책 선택:
        // 보통 '장전 취소'는 진행 중만 취소하고, 이미 장전된 탄은 유지해도 됨.
        // 여기선 "진행 중 취소"만 의미로 두자.
        Debug.Log("[Loader] CeaseAction (장전 취소)");
    }

    private IEnumerator LoadRoutine(AmmoType type)
    {
        isLoading = true;
        isLoaded = false;

        loading01 = 0f;

        float t = 0f;
        float dur = reloadTime;

        Debug.Log($"[Loader] {type} 장전 시작 ({dur:0.0}s)");

        while (t < dur)
        {
            t += Time.deltaTime;
            loading01 = Mathf.Clamp01(t / dur);
            yield return null;
        }

        isLoading = false;
        loading01 = 1f;
        shellType = type;
        isLoaded = true;

        Debug.Log($"[Loader] {type} 장전 완료");
        co = null;
    }

    public bool GetIsReloading()
    {
        return isLoading;
    }
    public bool GetIsReloaded()
    {
        return isLoaded;
    }

 }
