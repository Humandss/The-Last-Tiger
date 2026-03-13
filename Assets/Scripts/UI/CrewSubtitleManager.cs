using UnityEngine;

public enum CrewSubtitleCue
{
    None,
    CommanderReady,
    DriverReady,
    GunnerReady,
    LoaderReady,
    Fire,
    Reload,
    TargetDown
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
    [SerializeField] private string fireTextKr = "사격!";
    [SerializeField] private string reloadTextKr = "재장전 완료!";
    [SerializeField] private string targetDownTextKr = "목표 격파!";

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
            case CrewSubtitleCue.Fire:
                speaker = Fallback(gunnerLabelKr, "포수");
                line = Fallback(fireTextKr, "사격!");
                return true;
            case CrewSubtitleCue.Reload:
                speaker = Fallback(loaderLabelKr, "장전수");
                line = Fallback(reloadTextKr, "재장전 완료!");
                return true;
            case CrewSubtitleCue.TargetDown:
                speaker = Fallback(commanderLabelKr, "지휘관");
                line = Fallback(targetDownTextKr, "목표 격파!");
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
