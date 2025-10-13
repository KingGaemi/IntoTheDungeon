#if UNITY_EDITOR
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Unity.Behaviour;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterDebugBehaviour))]
public class CharacterCoreEditor : Editor
{
    private CharacterDebugBehaviour _target;

    // Test 값들
    private int _testDamage = 10;
    private int _testHeal = 20;
    private float _testSpeedChange = 1.0f;

    private void OnEnable()
    {
        _target = (CharacterDebugBehaviour)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see runtime status", MessageType.Info);
            return;
        }

        if (_target.TrySnapshot(out var status))
        {
            // === Status 표시 ===
            EditorGUILayout.Space(10);
            DrawStatusSection(status);

            // === Test Functions ===
            EditorGUILayout.Space(10);
            DrawTestSection();

            // 자동 갱신
            Repaint();
        }
    }

    private void DrawStatusSection(StatusComponent status)
    {
        EditorGUILayout.LabelField("=== Runtime Status ===", EditorStyles.boldLabel);

        // HP with Progress Bar
        float hpRatio = status.HpRatio;
        Color prevColor = GUI.color;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("HP", GUILayout.Width(100));
        EditorGUILayout.LabelField($"{status.CurrentHp} / {status.MaxHp}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        Rect rect = EditorGUILayout.GetControlRect(false, 20);

        // HP 색상 변경 (낮으면 빨강)
        if (hpRatio < 0.3f)
            GUI.color = Color.red;
        else if (hpRatio < 0.6f)
            GUI.color = Color.yellow;
        else
            GUI.color = Color.green;

        EditorGUI.ProgressBar(rect, hpRatio, $"{hpRatio:P0}");
        GUI.color = prevColor;

        // 다른 스탯들
        DrawStatField("Damage", status.Damage.ToString());
        DrawStatField("Armor", status.Armor.ToString());
        DrawStatField("Attack Speed", $"{status.AttackSpeed:F2}");
        DrawStatField("Movement Speed", $"{status.MovementSpeed:F2}");
        DrawStatField("Alive", status.IsAlive ? "Yes" : "Dead");
    }

    private void DrawStatField(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(120));
        EditorGUILayout.LabelField(value);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTestSection()
    {
        EditorGUILayout.LabelField("=== Test Functions ===", EditorStyles.boldLabel);

        // Damage Test
        EditorGUILayout.BeginHorizontal();
        _testDamage = EditorGUILayout.IntField("Damage", _testDamage, GUILayout.Width(200));
        if (GUILayout.Button("Apply"))
        {
            _target.ApplyDamage(_testDamage);
        }
        EditorGUILayout.EndHorizontal();

        // Heal Test
        EditorGUILayout.BeginHorizontal();
        _testHeal = EditorGUILayout.IntField("Heal", _testHeal, GUILayout.Width(200));
        if (GUILayout.Button("Apply"))
        {
            _target.ApplyHeal(_testHeal);
        }
        EditorGUILayout.EndHorizontal();

        // Speed Test
        EditorGUILayout.BeginHorizontal();
        _testSpeedChange = EditorGUILayout.FloatField("Speed Change", _testSpeedChange, GUILayout.Width(200));
        if (GUILayout.Button("Attack Speed"))
        {
            _target.AddAttackSpeed(_testSpeedChange);
        }
        if (GUILayout.Button("Movement Speed"))
        {
            _target.AddMoveSpeed(_testSpeedChange);
        }
        EditorGUILayout.EndHorizontal();

        // Quick Buttons
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Kill"))
        {
            _target.ApplyDamage(9999);
        }

        EditorGUILayout.EndHorizontal();
    }
}
#endif