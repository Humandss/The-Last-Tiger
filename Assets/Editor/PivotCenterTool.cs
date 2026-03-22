using UnityEngine;
using UnityEditor;

/// <summary>
/// 선택한 GameObject의 피벗을 재정렬합니다.
/// - 기준 렌더러를 직접 고를 수 있어서 포신/포탑 따로 계산 가능
/// Tools → Center Pivot to Mesh Bounds
/// </summary>
public class PivotCenterTool : EditorWindow
{
    enum AxisMode { XYZ, XZ_Only, X_Only }

    Renderer[]  _childRenderers;
    bool[]      _include;
    AxisMode    _axisMode = AxisMode.XZ_Only;
    GameObject  _target;
    Vector2     _scroll;

    [MenuItem("Tools/Center Pivot to Mesh Bounds")]
    static void Open()
    {
        var win = GetWindow<PivotCenterTool>("Pivot Center Tool");
        win.minSize = new Vector2(320, 280);
        win.Refresh();
        win.Show();
    }

    void Refresh()
    {
        _target = Selection.activeGameObject;
        if (_target == null) return;
        _childRenderers = _target.GetComponentsInChildren<Renderer>();
        _include = new bool[_childRenderers.Length];
        for (int i = 0; i < _include.Length; i++) _include[i] = true;
    }

    void OnSelectionChange() => Refresh();

    void OnGUI()
    {
        EditorGUILayout.Space(6);

        if (_target == null)
        {
            EditorGUILayout.HelpBox("Hierarchy에서 피벗 오브젝트를 선택하세요.", MessageType.Info);
            if (GUILayout.Button("선택 새로고침")) Refresh();
            return;
        }

        EditorGUILayout.LabelField($"대상: {_target.name}", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ── 축 선택 ──────────────────────────────────────
        EditorGUILayout.LabelField("이동할 축", EditorStyles.miniBoldLabel);
        _axisMode = (AxisMode)EditorGUILayout.EnumPopup(_axisMode);
        EditorGUILayout.HelpBox(
            _axisMode == AxisMode.XYZ   ? "X, Y, Z 모두 이동" :
            _axisMode == AxisMode.XZ_Only ? "X, Z만 이동 (Y 고정) ← 포탑 회전축 추천" :
                                           "X만 이동 (좌우 중앙만 맞춤)",
            MessageType.None);

        EditorGUILayout.Space(8);

        // ── 렌더러 선택 ──────────────────────────────────
        EditorGUILayout.LabelField("바운드 계산에 포함할 렌더러 (포신 제외 가능)", EditorStyles.miniBoldLabel);

        if (_childRenderers == null || _childRenderers.Length == 0)
        {
            EditorGUILayout.HelpBox("자식 렌더러 없음", MessageType.Warning);
        }
        else
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(120));
            for (int i = 0; i < _childRenderers.Length; i++)
            {
                if (_childRenderers[i] == null) continue;
                _include[i] = EditorGUILayout.ToggleLeft(
                    _childRenderers[i].gameObject.name, _include[i]);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 선택"))   for (int i = 0; i < _include.Length; i++) _include[i] = true;
            if (GUILayout.Button("전체 해제"))   for (int i = 0; i < _include.Length; i++) _include[i] = false;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);

        // ── 실행 ─────────────────────────────────────────
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("피벗 중앙 맞추기", GUILayout.Height(36)))
            Apply();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("Ctrl+Z 로 되돌릴 수 있습니다.", MessageType.None);
    }

    void Apply()
    {
        if (_target == null || _childRenderers == null) return;

        // 선택된 렌더러들로 바운드 계산
        Bounds? bounds = null;
        for (int i = 0; i < _childRenderers.Length; i++)
        {
            if (!_include[i] || _childRenderers[i] == null) continue;
            if (bounds == null) bounds = _childRenderers[i].bounds;
            else                { var b = bounds.Value; b.Encapsulate(_childRenderers[i].bounds); bounds = b; }
        }

        if (bounds == null)
        {
            EditorUtility.DisplayDialog("오류", "포함할 렌더러를 하나 이상 선택하세요.", "확인");
            return;
        }

        Vector3 center = bounds.Value.center;
        Vector3 current = _target.transform.position;

        // 축 모드에 따라 이동량 결정
        Vector3 newPos = current;
        if (_axisMode == AxisMode.XYZ)   newPos = center;
        if (_axisMode == AxisMode.XZ_Only) { newPos.x = center.x; newPos.z = center.z; }
        if (_axisMode == AxisMode.X_Only)  { newPos.x = center.x; }

        Vector3 delta = newPos - current;
        if (delta.sqrMagnitude < 0.00001f) { Debug.Log("[PivotCenter] 이미 중앙입니다."); return; }

        // Undo 등록
        Undo.SetCurrentGroupName("Center Pivot");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(_target.transform, "Move Pivot");
        _target.transform.position = newPos;

        foreach (Transform child in _target.transform)
        {
            Undo.RecordObject(child, "Offset Child");
            child.position -= delta;
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"[PivotCenter] '{_target.name}' 피벗 → {newPos}  (delta: {delta})");
    }
}
