// Editor/EntityInspectorWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using IntoTheDungeon.Editor.ECS;
using IntoTheDungeon.Unity.Bridge;

public class EntityInspectorWindow : EditorWindow
{
    WorldViewer _viewer;
    Vector2 _scrollPos;
    
    [MenuItem("ECS/Entity Inspector")]
    static void ShowWindow()
    {
        GetWindow<EntityInspectorWindow>("Entity Inspector");
    }
    
    void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }
    
    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }
    
    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            RefreshViewer();
    }
    
    void RefreshViewer()
    {


       var world = ViewBridgeRuntimeBridge.World; // 가져오기
        if (world != null)
            _viewer = new WorldViewer(world);
        else
            _viewer = null;
    }
    
    void OnGUI()
    {
        // Play Mode 아니면 경고
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to inspect entities", MessageType.Info);
            return;
        }
        if (_viewer == null)
        {
            EditorGUILayout.HelpBox("ViewBridge/World not available", MessageType.Warning);
            if (GUILayout.Button("Find Now"))
            {
                // 최후의 수단: 검색
                var vb = FindFirstObjectByType<ViewBridge>(FindObjectsInactive.Include);
                if (vb != null) _viewer = new WorldViewer(vb.World);
            }
            return;
        }
        
        DrawContent();
    }
    
    void DrawContent()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField($"Entities: {_viewer.EntityCount}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"Archetypes: {_viewer.ArchetypeCount}", GUILayout.Width(120));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            Repaint();
        EditorGUILayout.EndHorizontal();
        
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        
        // Archetype 목록 표시
        foreach (var archetypeView in _viewer.GetArchetypeViews())
        {
            DrawArchetype(archetypeView);
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    void DrawArchetype(ArchetypeView archetypeView)
    {
        EditorGUILayout.BeginVertical("box");
        
        // Archetype 헤더
        EditorGUILayout.LabelField(archetypeView.GetSignature(), EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Chunks: {archetypeView.ChunkCount}");
        EditorGUILayout.LabelField($"Memory: {archetypeView.EstimatedMemoryBytes / 1024f:F1} KB");
        EditorGUILayout.LabelField($"Avg Utilization: {archetypeView.AverageChunkUtilization:F1}%");
        
        EditorGUI.indentLevel++;
        
        // Chunk 목록
        foreach (var chunkView in archetypeView.Chunks)
        {
            DrawChunk(chunkView);
        }
        
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }
    
    void DrawChunk(ChunkView chunkView)
    {
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.LabelField($"Chunk {chunkView.ChunkIndex}", GUILayout.Width(80));
        EditorGUILayout.LabelField($"{chunkView.EntityCount}/{chunkView.Capacity}", GUILayout.Width(60));
        
        // Progress Bar
        var rect = GUILayoutUtility.GetRect(100, 18);
        EditorGUI.ProgressBar(rect, chunkView.UtilizationPercent / 100f, $"{chunkView.UtilizationPercent:F0}%");
        
        if (chunkView.IsFull)
            EditorGUILayout.LabelField("FULL", GUILayout.Width(40));
        
        EditorGUILayout.EndHorizontal();
    }
    
    void Update()
    {
        // Play Mode에서 자동 갱신
        if (EditorApplication.isPlaying)
            Repaint();
    }
}
#endif