#if false
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 기본 인스펙터 표시
        DrawDefaultInspector();

        GameManager gm = (GameManager)target;

        // 버튼 추가
        if (GUILayout.Button("Game Start"))
        {
            gm.GameStart();
        }

        if (GUILayout.Button("Stage Start"))
        {
            gm.StageStart();
        }
    }
}
#endif