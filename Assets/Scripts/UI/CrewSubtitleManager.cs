using UnityEngine;

public enum CrewSubtitleCue
{
    None,
    CommanderReady,
    DriverReady,
    GunnerReady,
    LoaderReady,
    LoadAP,
    LoadHE,
    Fire,
    Reload,
    TargetDown,
    Penetrated,
    NoPenetration,
    Ricocheted,
    EnemyInSight,
    DriverDead,
    MGDead,
    GunnerDead,
    LoaderDead
}

public class CrewSubtitleManager : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private CrewSubtitleOverlay overlay;
    [SerializeField] private float minDisplaySeconds = 1.2f;

    [Header("Speakers (KR)")]
    [SerializeField] private string commanderLabelKr = "지휘관";
    [SerializeField] private string driverLabelKr = "조종수";
    [SerializeField] private string gunnerLabelKr = "포수";
    [SerializeField] private string loaderLabelKr = "장전수";

    [Header("Lines (KR)")]
    [SerializeField] private string commanderReadyTextKr = "전원 전투 준비.";
    [SerializeField] private string driverReadyTextKr = "조종수, 준비 완료!";
    [SerializeField] private string gunnerReadyTextKr = "포수, 준비 완료!";
    [SerializeField] private string loaderReadyTextKr = "장전수, 준비 완료!";
    [SerializeField] private string loadAPTextKr = "철갑탄 장전!";
    [SerializeField] private string loadHETextKr = "고폭탄 장전!";
    [SerializeField] private string fireTextKr = "사격!";
    [SerializeField] private string reloadTextKr = "재장전 완료!";
    [SerializeField] private string targetDownTextKr = "목표 격파!";
    [SerializeField] private string penetratedTextKr = "관통!";
    [SerializeField] private string noPenetrationTextKr = "비관통!";
    [SerializeField] private string ricochetedTextKr = "도탄!";
    [SerializeField] private string enemyInSightTextKr = "적 전차 발견!";
    [SerializeField] private string driverDeadTextKr = "조종수 사망!";
    [SerializeField] private string mgDeadTextKr = "전방 사수 사망!";
    [SerializeField] private string gunnerDeadTextKr = "포수 사망!";
    [SerializeField] private string loaderDeadTextKr = "장전수 사망!";

    private void Awake()
    {
        if (overlay == null)
            overlay = CrewSubtitleOverlay.GetOrCreate();
    }

    public void Show(CrewSubtitleCue cue, float duration)
    {
        if (cue == CrewSubtitleCue.None) return;

        if (overlay == null)
            overlay = CrewSubtitleOverlay.GetOrCreate();

        if (overlay == null) return;

        if (!TryResolve(cue, out string speaker, out string line)) return;
        float showFor = Mathf.Max(minDisplaySeconds, duration);
        overlay.ShowLine(speaker, line, showFor);
    }

    private bool TryResolve(CrewSubtitleCue cue, out string speaker, out string line)
    {
        speaker = string.Empty;
        line = string.Empty;

        switch (cue)
        {
            case CrewSubtitleCue.CommanderReady:
                speaker = Fallback(commanderLabelKr, "지휘관");
                line = Fallback(commanderReadyTextKr, "전원 전투 준비.");
                return true;
            case CrewSubtitleCue.DriverReady:
                speaker = Fallback(driverLabelKr, "조종수");
                line = Fallback(driverReadyTextKr, "조종수, 준비 완료!");
                return true;
            case CrewSubtitleCue.GunnerReady:
                speaker = Fallback(gunnerLabelKr, "포수");
                line = Fallback(gunnerReadyTextKr, "포수, 준비 완료!");
                return true;
            case CrewSubtitleCue.LoaderReady:
                speaker = Fallback(loaderLabelKr, "장전수");
                line = Fallback(loaderReadyTextKr, "장전수, 준비 완료!");
                return true;
            case CrewSubtitleCue.LoadAP:
                speaker = Fallback(commanderLabelKr, "지휘관");
                line = Fallback(loadAPTextKr, "철갑탄 장전!.");
                return true;
            case CrewSubtitleCue.LoadHE:
                speaker = Fallback(commanderLabelKr, "지휘관");
                line = Fallback(loadHETextKr, "고폭탄 장전!");
                return true;
            case CrewSubtitleCue.Fire:
                speaker = Fallback(gunnerLabelKr, "포수");
                line = Fallback(fireTextKr, "발사!");
                return true;
            case CrewSubtitleCue.Reload:
                speaker = Fallback(loaderLabelKr, "장전수");
                line = Fallback(reloadTextKr, "재장전 완료!");
                return true;
            case CrewSubtitleCue.TargetDown:
                speaker = Fallback(commanderLabelKr, "지휘관");
                line = Fallback(targetDownTextKr, "목표 격파!");
                return true;
            case CrewSubtitleCue.Penetrated:
                speaker = Fallback(gunnerLabelKr, "포수");
                line = Fallback(penetratedTextKr, "적 전차 관통!");
                return true;
            case CrewSubtitleCue.NoPenetration:
                speaker = Fallback(gunnerLabelKr, "포수");
                line = Fallback(noPenetrationTextKr, "적 전차 비관통!");
                return true;
            case CrewSubtitleCue.Ricocheted:
                speaker = Fallback(gunnerLabelKr, "포수");
                line = Fallback(ricochetedTextKr, "적 전차 도탄!");
                return true;
            case CrewSubtitleCue.EnemyInSight:
                speaker = Fallback(commanderLabelKr, "전차장");
                line = Fallback(enemyInSightTextKr, "적 전차 발견!");
                return true;
            case CrewSubtitleCue.GunnerDead:
                speaker = Fallback(commanderLabelKr, "전차장");
                line = Fallback(gunnerDeadTextKr, "포수 사망!");
                return true;
            case CrewSubtitleCue.DriverDead:
                speaker = Fallback(commanderLabelKr, "전차장");
                line = Fallback(driverDeadTextKr, "조종수 사망!");
                return true;
            case CrewSubtitleCue.MGDead:
                speaker = Fallback(commanderLabelKr, "전차장");
                line = Fallback(mgDeadTextKr, "전방 사수 사망!!");
                return true;
            case CrewSubtitleCue.LoaderDead:
                speaker = Fallback(commanderLabelKr, "전차장");
                line = Fallback(loaderDeadTextKr, "장전수 사망!");
                return true;
            default:
                return false;
        }
    }

    private static string Fallback(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
}
