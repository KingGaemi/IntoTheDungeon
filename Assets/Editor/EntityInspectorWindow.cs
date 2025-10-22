// Editor/EntityInspectorWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using IntoTheDungeon.Editor.ECS;
using IntoTheDungeon.Unity;
using IntoTheDungeon.Core.Abstractions.World;

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
        UnityBootstrapper.WorldChanged += OnWorldChanged;

        // 이미 실행 중이면 즉시 갱신
        if (EditorApplication.isPlaying)
            RefreshViewer();
    }

    void OnDisable()
    {
        UnityBootstrapper.WorldChanged -= OnWorldChanged;
    }

    void OnWorldChanged(IWorld world)
    {
        _viewer = world != null ? new WorldViewer(world) : null;
        Repaint();
    }

    void RefreshViewer()
    {
        var bootstrapper = Object.FindFirstObjectByType<UnityBootstrapper>(FindObjectsInactive.Include);

        if (bootstrapper == null)
        {
            Debug.LogWarning("[EntityInspector] UnityBootstrapper not found");
            _viewer = null;
            return;
        }

        var world = bootstrapper.World;
        if (world == null)
        {
            Debug.LogWarning("[EntityInspector] World not initialized yet");
            _viewer = null;
            return;
        }

        _viewer = new WorldViewer(world);
        Debug.Log("[EntityInspector] Viewer refreshed");
    }

    void OnGUI()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to inspect entities", MessageType.Info);
            return;
        }

        if (_viewer == null)
        {
            EditorGUILayout.HelpBox("World not available", MessageType.Warning);
            if (GUILayout.Button("Find Now"))
                RefreshViewer();
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

        foreach (var archetypeView in _viewer.GetArchetypeViews())
        {
            DrawArchetype(archetypeView);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawArchetype(ArchetypeView archetypeView)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(archetypeView.GetSignature(), EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Chunks: {archetypeView.ChunkCount}");
        EditorGUILayout.LabelField($"Memory: {archetypeView.EstimatedMemoryBytes / 1024f:F1} KB");
        EditorGUILayout.LabelField($"Avg Utilization: {archetypeView.AverageChunkUtilization:F1}%");

        EditorGUI.indentLevel++;
        foreach (var chunkView in archetypeView.Chunks)
            DrawChunk(chunkView);
        EditorGUI.indentLevel--;

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    void DrawChunk(ChunkView chunkView)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Chunk {chunkView.ChunkIndex}", GUILayout.Width(80));
        EditorGUILayout.LabelField($"{chunkView.EntityCount}/{chunkView.Capacity}", GUILayout.Width(60));

        var rect = GUILayoutUtility.GetRect(100, 18);
        EditorGUI.ProgressBar(rect, chunkView.UtilizationPercent / 100f, $"{chunkView.UtilizationPercent:F0}%");

        if (chunkView.IsFull)
            EditorGUILayout.LabelField("FULL", GUILayout.Width(40));

        EditorGUILayout.EndHorizontal();
    }

    void Update()
    {
        if (EditorApplication.isPlaying)
            Repaint();
    }
}
#endif