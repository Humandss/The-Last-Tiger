using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private float keepAliveSeconds = 2.0f; // 맞은 뒤 몇 초 표시할지
    [SerializeField] private Vector2 guiPos = new Vector2(12, 12);

    private ModuleManager active;
    private float ttl;
    private string text;

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

        if (ttl > 0f) ttl -= Time.deltaTime;
        else active = null;
    }

    public void Show(ModuleManager mgr, string text)
    {
        if (!show) return;

        active = mgr;
        this.text = text;
        ttl = keepAliveSeconds;
    }

    private void OnGUI()
    {
        if (!show) return;
        if (active == null) return;
        if (string.IsNullOrEmpty(text)) return;

        style = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            fontSize = 14
        };

        GUI.Label(new Rect(guiPos.x, guiPos.y, 900, 900), text, style);
    }
}
