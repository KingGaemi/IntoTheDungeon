using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

public class TilemapRecoveryTool : EditorWindow
{
    private Tilemap targetTilemap;
    private Vector2 scrollPosition;
    private List<TileInfo> brokenTiles = new List<TileInfo>();
    private bool scanComplete = false;
    
    private class TileInfo
    {
        public Vector3Int position;
        public TileBase tile;
        public Sprite sprite;
        public bool isNull;
        public bool isMissing;
        public TileBase replacementTile; // 드래그 앤 드롭으로 할당할 타일
    }

    [MenuItem("Tools/Tilemap Recovery Tool")]
    public static void ShowWindow()
    {
        GetWindow<TilemapRecoveryTool>("Tilemap Recovery");
    }

    void OnGUI()
    {
        GUILayout.Label("Tilemap 복구 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Tilemap 선택
        targetTilemap = (Tilemap)EditorGUILayout.ObjectField(
            "대상 Tilemap:", 
            targetTilemap, 
            typeof(Tilemap), 
            true
        );

        EditorGUILayout.Space();

        // 스캔 버튼
        EditorGUI.BeginDisabledGroup(targetTilemap == null);
        if (GUILayout.Button("깨진 타일 스캔", GUILayout.Height(30)))
        {
            ScanBrokenTiles();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();

        // 스캔 결과 표시
        if (scanComplete)
        {
            EditorGUILayout.HelpBox(
                $"발견된 문제: {brokenTiles.Count}개", 
                brokenTiles.Count > 0 ? MessageType.Warning : MessageType.Info
            );

            if (brokenTiles.Count > 0)
            {
                EditorGUILayout.Space();
                
                // 일괄 작업 버튼들
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("할당된 타일로 일괄 교체", GUILayout.Height(25)))
                {
                    ReplaceAllAssignedTiles();
                }
                
                if (GUILayout.Button("모든 깨진 타일 제거", GUILayout.Height(25)))
                {
                    RemoveAllBrokenTiles();
                }
                
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Tilemap 압축 (빈 영역 정리)", GUILayout.Height(25)))
                {
                    CompressTilemap();
                }

                EditorGUILayout.Space();
                GUILayout.Label("깨진 타일 목록:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "각 타일에 교체할 Tile을 드래그 앤 드롭하세요!", 
                    MessageType.Info
                );

                // 스크롤 뷰로 타일 목록 표시
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
                
                for (int i = 0; i < brokenTiles.Count && i < 100; i++)
                {
                    var tileInfo = brokenTiles[i];
                    
                    EditorGUILayout.BeginVertical("box");
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"위치: {tileInfo.position}", GUILayout.Width(150));
                    
                    if (tileInfo.isNull)
                    {
                        GUILayout.Label("타입: Null 타일", GUILayout.Width(120));
                    }
                    else if (tileInfo.isMissing)
                    {
                        GUILayout.Label("타입: 참조 손실", GUILayout.Width(120));
                    }

                    if (GUILayout.Button("이동", GUILayout.Width(50)))
                    {
                        FocusOnTile(tileInfo.position);
                    }

                    if (GUILayout.Button("제거", GUILayout.Width(50)))
                    {
                        RemoveTileAt(tileInfo.position);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                    
                    // 드래그 앤 드롭 필드
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("교체할 타일:", GUILayout.Width(100));
                    
                    TileBase newTile = (TileBase)EditorGUILayout.ObjectField(
                        tileInfo.replacementTile, 
                        typeof(TileBase), 
                        false,
                        GUILayout.Width(200)
                    );
                    
                    // 타일이 변경되었으면 저장
                    if (newTile != tileInfo.replacementTile)
                    {
                        tileInfo.replacementTile = newTile;
                    }
                    
                    // 개별 교체 버튼
                    EditorGUI.BeginDisabledGroup(tileInfo.replacementTile == null);
                    if (GUILayout.Button("적용", GUILayout.Width(60)))
                    {
                        ReplaceTile(tileInfo);
                    }
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUILayout.EndHorizontal();
                    
                    // 미리보기 (할당된 타일이 있는 경우)
                    if (tileInfo.replacementTile != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("미리보기:", GUILayout.Width(100));
                        
                        Sprite previewSprite = null;
                        if (tileInfo.replacementTile is Tile)
                        {
                            previewSprite = ((Tile)tileInfo.replacementTile).sprite;
                        }
                        
                        if (previewSprite != null)
                        {
                            Texture2D texture = AssetPreview.GetAssetPreview(previewSprite);
                            if (texture != null)
                            {
                                GUILayout.Label(texture, GUILayout.Width(50), GUILayout.Height(50));
                            }
                        }
                        
                        GUILayout.Label(tileInfo.replacementTile.name, EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }

                if (brokenTiles.Count > 100)
                {
                    EditorGUILayout.HelpBox($"... 그 외 {brokenTiles.Count - 100}개 더 있음 (처음 100개만 표시)", MessageType.Info);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "사용 방법:\n" +
            "1. 깨진 타일 스캔\n" +
            "2. 각 타일에 교체할 Tile을 드래그 앤 드롭\n" +
            "3. '적용' 버튼으로 개별 교체 또는 '일괄 교체'로 한번에 적용\n\n" +
            "작업 전 프로젝트를 백업하세요!",
            MessageType.Info
        );
    }

    private void ScanBrokenTiles()
    {
        brokenTiles.Clear();
        
        if (targetTilemap == null)
        {
            Debug.LogError("Tilemap이 선택되지 않았습니다.");
            return;
        }

        BoundsInt bounds = targetTilemap.cellBounds;
        int totalTiles = 0;
        int brokenCount = 0;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int z = bounds.zMin; z < bounds.zMax; z++)
                {
                    Vector3Int pos = new Vector3Int(x, y, z);
                    TileBase tile = targetTilemap.GetTile(pos);

                    if (tile != null)
                    {
                        totalTiles++;
                        
                        bool isBroken = false;
                        bool isNull = false;
                        bool isMissing = false;

                        try
                        {
                            Sprite sprite = targetTilemap.GetSprite(pos);
                            
                            if (sprite == null && tile != null)
                            {
                                isBroken = true;
                                isMissing = true;
                            }
                            
                            if (string.IsNullOrEmpty(tile.name) || tile.name == "Tile")
                            {
                                isBroken = true;
                                isNull = true;
                            }
                        }
                        catch
                        {
                            isBroken = true;
                            isMissing = true;
                        }

                        if (isBroken)
                        {
                            brokenCount++;
                            brokenTiles.Add(new TileInfo
                            {
                                position = pos,
                                tile = tile,
                                sprite = targetTilemap.GetSprite(pos),
                                isNull = isNull,
                                isMissing = isMissing,
                                replacementTile = null
                            });
                        }
                    }
                }
            }
        }

        scanComplete = true;
        Debug.Log($"스캔 완료: 전체 {totalTiles}개 타일 중 {brokenCount}개의 문제 발견");
    }

    private void ReplaceTile(TileInfo tileInfo)
    {
        if (targetTilemap == null || tileInfo.replacementTile == null)
            return;

        Undo.RecordObject(targetTilemap, "Replace Tile");
        targetTilemap.SetTile(tileInfo.position, tileInfo.replacementTile);
        EditorUtility.SetDirty(targetTilemap);

        Debug.Log($"타일 교체 완료: {tileInfo.position} -> {tileInfo.replacementTile.name}");
        
        // 교체된 타일을 리스트에서 제거
        brokenTiles.Remove(tileInfo);
        
        if (brokenTiles.Count == 0)
        {
            EditorUtility.DisplayDialog("완료!", "모든 타일 교체가 완료되었습니다.", "확인");
        }
    }

    private void ReplaceAllAssignedTiles()
    {
        if (targetTilemap == null) return;

        int replacedCount = 0;
        List<TileInfo> tilesToRemove = new List<TileInfo>();

        Undo.RecordObject(targetTilemap, "Replace All Tiles");

        foreach (var tileInfo in brokenTiles)
        {
            if (tileInfo.replacementTile != null)
            {
                targetTilemap.SetTile(tileInfo.position, tileInfo.replacementTile);
                replacedCount++;
                tilesToRemove.Add(tileInfo);
            }
        }

        // 교체된 타일들을 리스트에서 제거
        foreach (var tile in tilesToRemove)
        {
            brokenTiles.Remove(tile);
        }

        EditorUtility.SetDirty(targetTilemap);
        Debug.Log($"{replacedCount}개의 타일을 교체했습니다.");
        
        if (replacedCount > 0)
        {
            EditorUtility.DisplayDialog(
                "교체 완료", 
                $"{replacedCount}개의 타일을 교체했습니다.\n남은 깨진 타일: {brokenTiles.Count}개", 
                "확인"
            );
        }
        else
        {
            EditorUtility.DisplayDialog(
                "알림", 
                "교체할 타일이 할당되지 않았습니다.\n각 타일에 교체할 Tile을 드래그 앤 드롭하세요.", 
                "확인"
            );
        }
    }

    private void RemoveAllBrokenTiles()
    {
        if (targetTilemap == null) return;

        if (!EditorUtility.DisplayDialog(
            "확인", 
            $"{brokenTiles.Count}개의 깨진 타일을 모두 제거하시겠습니까?", 
            "제거", 
            "취소"))
        {
            return;
        }

        Undo.RecordObject(targetTilemap, "Remove Broken Tiles");

        int removed = 0;
        foreach (var tileInfo in brokenTiles)
        {
            targetTilemap.SetTile(tileInfo.position, null);
            removed++;
        }

        targetTilemap.CompressBounds();
        EditorUtility.SetDirty(targetTilemap);

        Debug.Log($"{removed}개의 깨진 타일을 제거했습니다.");
        ScanBrokenTiles();
    }

    private void RemoveTileAt(Vector3Int position)
    {
        if (targetTilemap == null) return;

        Undo.RecordObject(targetTilemap, "Remove Tile");
        targetTilemap.SetTile(position, null);
        EditorUtility.SetDirty(targetTilemap);

        Debug.Log($"타일 제거: {position}");
        ScanBrokenTiles();
    }

    private void CompressTilemap()
    {
        if (targetTilemap == null) return;

        Undo.RecordObject(targetTilemap, "Compress Tilemap");
        targetTilemap.CompressBounds();
        EditorUtility.SetDirty(targetTilemap);

        Debug.Log("Tilemap 압축 완료");
    }

    private void FocusOnTile(Vector3Int position)
    {
        if (targetTilemap == null) return;

        Selection.activeGameObject = targetTilemap.gameObject;
        SceneView.lastActiveSceneView.Frame(
            new Bounds(targetTilemap.CellToWorld(position), Vector3.one * 2f), 
            false
        );
    }
}

// 추가 유틸리티: 타일 일괄 재연결 도구
public class TileReconnector : EditorWindow
{
    private Tilemap tilemap;
    private Sprite[] sourceSprites;
    private Vector2 scrollPos;

    [MenuItem("Tools/Tile Reconnector")]
    public static void ShowWindow()
    {
        GetWindow<TileReconnector>("Tile Reconnector");
    }

    void OnGUI()
    {
        GUILayout.Label("타일-스프라이트 재연결", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap:", tilemap, typeof(Tilemap), true);

        EditorGUILayout.Space();
        GUILayout.Label("재연결할 스프라이트들을 드래그하세요:", EditorStyles.boldLabel);

        SerializedObject so = new SerializedObject(this);
        SerializedProperty spritesProperty = so.FindProperty("sourceSprites");
        EditorGUILayout.PropertyField(spritesProperty, true);
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(tilemap == null || sourceSprites == null || sourceSprites.Length == 0);
        if (GUILayout.Button("스프라이트로 새 타일 생성 및 교체", GUILayout.Height(30)))
        {
            ReconnectTiles();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "이 도구는 선택한 스프라이트들로 새 Tile 에셋을 생성하고\n" +
            "Tilemap의 타일들을 순차적으로 교체합니다.",
            MessageType.Info
        );
    }

    private void ReconnectTiles()
    {
        if (tilemap == null || sourceSprites == null || sourceSprites.Length == 0)
            return;

        string path = EditorUtility.SaveFolderPanel("타일 저장 위치", "Assets", "");
        if (string.IsNullOrEmpty(path))
            return;

        path = "Assets" + path.Substring(Application.dataPath.Length);

        List<Tile> createdTiles = new List<Tile>();
        
        for (int i = 0; i < sourceSprites.Length; i++)
        {
            if (sourceSprites[i] == null) continue;

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sourceSprites[i];
            tile.name = sourceSprites[i].name;

            string tilePath = $"{path}/{tile.name}.asset";
            AssetDatabase.CreateAsset(tile, tilePath);
            createdTiles.Add(tile);

            EditorUtility.DisplayProgressBar("타일 생성 중", $"{i + 1}/{sourceSprites.Length}", (float)i / sourceSprites.Length);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{createdTiles.Count}개의 타일을 생성했습니다: {path}");
        EditorUtility.DisplayDialog("완료", $"{createdTiles.Count}개의 타일이 생성되었습니다.\n수동으로 Tilemap에 배치하세요.", "확인");
    }
}