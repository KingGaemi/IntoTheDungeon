using UnityEngine;

namespace IntoTheDungeon.Unity.World
{
    /// <summary>
    /// 2.5D 바닥 그림이 생기기 전까지 쓰는 임시 바닥. WalkAreaAuthoring의 걷는 띠 위치에
    /// 코드로 그린 돌바닥을 깐다(안쪽으로 갈수록 줄 높이·타일 폭이 줄고 어두워져 원근감).
    /// 플레이할 때만 만든다. 진짜 바닥 그림을 넣으면 이 컴포넌트를 지우면 된다. 스테이지마다 색이 다르다(2: 이끼 낀 초록).
    /// </summary>
    [RequireComponent(typeof(WalkAreaAuthoring))]
    public sealed class PlaceholderFloor : MonoBehaviour
    {
        [Header("걷는 띠보다 넓게 까는 여유(월드 단위)")]
        [SerializeField] float frontMargin = 0.6f;
        [SerializeField] float backMargin = 0.3f;
        [SerializeField] float sideMargin = 1f;

        [Header("모양")]
        [SerializeField] int pixelsPerUnit = 24;
        [Tooltip("가장 앞 줄 타일 크기(월드 단위)")]
        [SerializeField] Vector2 frontTileSize = new(1.4f, 0.9f);
        [Tooltip("안쪽으로 한 줄 갈 때마다 줄 높이 비율")]
        [Range(0.5f, 1f)]
        [SerializeField] float rowShrink = 0.85f;
        [Tooltip("줄 높이가 이보다 작아지지 않는다(너무 얇은 줄이 쌓이지 않게)")]
        [SerializeField] float minRowHeight = 0.45f;
        [Tooltip("가장 안쪽 줄의 타일 폭이 이만큼 좁아진다(줄 단위로 적용해 줄 안의 줄눈은 똑바로)")]
        [Range(0f, 0.8f)]
        [SerializeField] float convergence = 0.3f;
        [SerializeField] Color stone = new Color32(70, 63, 76, 255);
        [SerializeField] Color seam = new Color32(36, 32, 42, 255);
        [Header("스테이지 2(식인 식물 숲): 이끼 낀 돌")]
        [SerializeField] Color stone2 = new Color32(56, 74, 52, 255);
        [SerializeField] Color seam2 = new Color32(26, 38, 28, 255);
        [Tooltip("가장 안쪽이 이만큼 어두워진다")]
        [Range(0f, 1f)]
        [SerializeField] float backDarken = 0.3f;

        const int SortingOrder = -30000;   // Default 레이어 맨 뒤: 방 타일보다 앞, 캐릭터·그림자보다 뒤

        GameObject _floor;
        int _builtStage = -1;

        void Awake() => Rebuild();

        // 같은 씬에서 다음 스테이지로 넘어가면(정비 → 지도) 바닥 색도 바꾼다
        void Update()
        {
            if (IntoTheDungeon.Features.Run.RunState.Current.Stage != _builtStage) Rebuild();
        }

        void Rebuild()
        {
            _builtStage = IntoTheDungeon.Features.Run.RunState.Current.Stage;
            if (_floor) Destroy(_floor);
            var area = GetComponent<WalkAreaAuthoring>();
            float x0 = area.MinX - sideMargin, x1 = area.MaxX + sideMargin;
            float y0 = area.MinDepth - frontMargin, y1 = area.MaxDepth + backMargin;

            var tex = Build(x1 - x0, y1 - y0);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, pixelsPerUnit);
            sprite.name = "PlaceholderFloor";

            var go = _floor = new GameObject("PlaceholderFloor");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x0, y0, transform.position.z);
            // 부모 스케일과 무관하게 월드 크기로
            var ps = transform.lossyScale;
            go.transform.localScale = new Vector3(1f / Safe(ps.x), 1f / Safe(ps.y), 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = 0;           // Default
            sr.sortingOrder = SortingOrder;
        }

        static float Safe(float v) => Mathf.Abs(v) < 1e-5f ? 1f : v;

        Texture2D Build(float widthUnits, float depthUnits)
        {
            int W = Mathf.Max(1, Mathf.RoundToInt(widthUnits * pixelsPerUnit));
            int H = Mathf.Max(1, Mathf.RoundToInt(depthUnits * pixelsPerUnit));
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];

            // 줄 경계(앞→안쪽, 월드 단위): 높이가 rowShrink배씩 줄어든다
            var rowEdges = new System.Collections.Generic.List<float> { 0f };
            float h = frontTileSize.y;
            while (rowEdges[^1] < depthUnits) { rowEdges.Add(rowEdges[^1] + h); h = Mathf.Max(minRowHeight, h * rowShrink); }
            int rowCount = rowEdges.Count - 1;

            float cx = widthUnits * 0.5f;
            var rng = new System.Random(12345);
            for (int y = 0; y < H; y++)
            {
                float d = (y + 0.5f) / pixelsPerUnit;              // 앞에서부터 깊이
                float t = Mathf.Clamp01(d / depthUnits);            // 0 앞 ~ 1 안쪽
                int row = 0;
                while (row + 1 < rowEdges.Count && rowEdges[row + 1] <= d) row++;
                float rowTop = rowEdges[Mathf.Min(row + 1, rowEdges.Count - 1)];
                bool hSeam = (rowTop - d) * pixelsPerUnit < 1f;    // 줄 위 경계 1px

                float rowT = (float)row / Mathf.Max(1, rowCount - 1);
                float squeeze = 1f - convergence * rowT;            // 안쪽 줄일수록 타일 폭이 좁아짐(줄 안에서는 일정)
                float tileW = frontTileSize.x * squeeze;
                float offset = (row % 2) * 0.5f * tileW;            // 줄마다 반 칸 엇갈림
                float shade = 1f - backDarken * t;

                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / pixelsPerUnit;
                    float ux = cx + (u - cx) * squeeze;             // 가운데로 모으기
                    float local = (ux + offset) % tileW;
                    if (local < 0) local += tileW;
                    bool vSeam = local / squeeze * pixelsPerUnit < 1f;

                    bool stage2 = _builtStage >= 2;
                    Color c = (hSeam || vSeam) ? (stage2 ? seam2 : seam) : (stage2 ? stone2 : stone);
                    if (!hSeam && !vSeam)
                    {
                        // 타일마다 살짝 다른 밝기(같은 타일은 같은 값)
                        int tileId = row * 1000 + Mathf.FloorToInt((ux + offset) / tileW);
                        float n = (unchecked((uint)tileId * 2654435761u) % 1000u) / 1000f;
                        c *= 0.92f + 0.12f * n;
                        if (rng.NextDouble() < 0.02) c *= 0.85f;    // 잔 얼룩
                    }
                    c *= shade; c.a = 1f;
                    px[y * W + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
