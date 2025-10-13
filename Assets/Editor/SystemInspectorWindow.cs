#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using IntoTheDungeon.Editor.ECS;
using IntoTheDungeon.Runtime;
using IntoTheDungeon.Unity;
public class ECSDebugWindow : EditorWindow
{
    private WorldViewer _viewer;
    private Vector2 _scrollPos;
    [MenuItem("ECS/System Inspector")]
    static void Open()
    {
        GetWindow<ECSDebugWindow>("System Inspector");
    }
    
    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to view ECS data", MessageType.Info);
            return;
        }
        

    
        
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        
        DrawSystemInfo();
        EditorGUILayout.Space(10);
        DrawEntityInfo();
        EditorGUILayout.Space(10);
        DrawArchetypeInfo();
        
        EditorGUILayout.EndScrollView();
        
        Repaint();
    }
    
    void DrawSystemInfo()
    {
        EditorGUILayout.LabelField("Systems", EditorStyles.boldLabel);
        

        
        EditorGUILayout.Space(5);
        
        foreach (var systemView in _viewer.GetSystemViews())
        {
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                EditorGUILayout.LabelField(systemView.ShortName, GUILayout.Width(200));
                EditorGUILayout.LabelField($"Priority: {systemView.Priority}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{systemView.LastExecutionTimeMs:F2}ms", GUILayout.Width(80));
                EditorGUILayout.LabelField(systemView.GetExecutionPhase(), GUILayout.Width(150));
            }
        }
    }
    
    void DrawEntityInfo()
    {
        EditorGUILayout.LabelField("Entities", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Count: {_viewer.EntityCount}");
        EditorGUILayout.LabelField($"Archetypes: {_viewer.ArchetypeCount}");
    }
    
    void DrawArchetypeInfo()
    {
        EditorGUILayout.LabelField("Archetypes", EditorStyles.boldLabel);
        
        foreach (var archetype in _viewer.GetArchetypeViews())
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"[{archetype.GetSignature()}]");
                EditorGUILayout.LabelField($"Chunks: {archetype.ChunkCount}, Utilization: {archetype.AverageChunkUtilization:F1}%");
            }
        }
    }
}
#endif