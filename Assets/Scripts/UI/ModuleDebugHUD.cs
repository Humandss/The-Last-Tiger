using UnityEngine;

public enum DebugHudChannel
{
    Player,
    Enemy,
}

public class ModuleDebugHUD : MonoBehaviour
{
    public static ModuleDebugHUD Instance { get; private set; }

    [Header("Display")]
    [SerializeField] private bool show = true;
    [SerializeField] private float keepAliveSeconds = 2.0f;
    [SerializeField] private Vector2 enemyGuiPos = new Vector2(12f, 12f);
    [SerializeField] private float playerLeft = 12f;
    [SerializeField] private float playerBottom = 12f;
    [SerializeField] private float blockWidth = 900f;
    [SerializeField] private float blockHeight = 220f;
    [SerializeField] private float lineSpacing = 1.25f;
    [SerializeField, Range(0.2f, 0.95f)] private float maxHeightRatio = 0.75f;

    private ModuleManager enemyActive;
    private float enemyTtl;
    private string enemyText;

    private ModuleManager playerActive;
    private float playerTtl;
    private string playerText;

    private GUIStyle style;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (!show) return;

        if (enemyTtl > 0f) enemyTtl -= Time.deltaTime;
        else enemyActive = null;

        if (playerTtl > 0f) playerTtl -= Time.deltaTime;
        else playerActive = null;
    }

    public void Show(ModuleManager mgr, string text, DebugHudChannel channel)
    {
        if (!show) return;

        if (channel == DebugHudChannel.Player)
        {
            playerActive = mgr;
            playerText = text;
            playerTtl = keepAliveSeconds;
            return;
        }

        enemyActive = mgr;
        enemyText = text;
        enemyTtl = keepAliveSeconds;
    }

    private void OnGUI()
    {
        if (!show) return;

        style = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            fontSize = 17
        };

        if (enemyActive != null && !string.IsNullOrEmpty(enemyText))
        {
            float h = GetAutoHeight(enemyText);
            GUI.Label(new Rect(enemyGuiPos.x, enemyGuiPos.y, blockWidth, h), enemyText, style);
        }

        if (playerActive != null && !string.IsNullOrEmpty(playerText))
        {
            float h = GetAutoHeight(playerText);
            float y = Screen.height - playerBottom - h;
            GUI.Label(new Rect(playerLeft, y, blockWidth, h), playerText, style);
        }
    }

    private float GetAutoHeight(string content)
    {
        if (string.IsNullOrEmpty(content))
            return blockHeight;

        int lines = 1;
        for (int i = 0; i < content.Length; i++)
            if (content[i] == '\n') lines++;

        float lineH = Mathf.Max(16f, style.fontSize * lineSpacing);
        float needed = 8f + (lines * lineH);
        float maxH = Mathf.Max(blockHeight, Screen.height * maxHeightRatio);
        return Mathf.Clamp(Mathf.Max(blockHeight, needed), blockHeight, maxH);
    }
}
