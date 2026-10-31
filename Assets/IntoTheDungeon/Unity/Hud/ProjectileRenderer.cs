using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Attack;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 투사체(ProjectileVisualComponent + HitboxComponent) 엔티티를 매 프레임 읽어 코드로 그린 스프라이트로 보여준다.
    /// 종류: 검기(초승달), 화살, 강화 화살(빛나는 큰 화살). 판정 박스 위치를 그대로 따라가고(화면 y = 깊이 + 높이),
    /// 검기는 수명 끝 무렵 옅어진다. 진짜 이펙트 그림이 생기면 종류별 그림만 바꾸면 된다.
    /// </summary>
    public sealed class ProjectileRenderer : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] int pixelsPerUnit = 40;
        [Tooltip("판정 높이(발)에서 검기 그림 중심까지 올리는 높이(가슴 높이쯤)")]
        [SerializeField] float waveHeight = 0.7f;
        [Tooltip("판정 높이(발)에서 화살까지 올리는 높이(활 높이쯤)")]
        [SerializeField] float arrowHeight = 0.55f;
        [SerializeField] Color edgeColor = new(1f, 1f, 1f, 1f);
        [SerializeField] Color glowColor = new(0.35f, 0.8f, 1f, 1f);

        IWorld _world;
        readonly Dictionary<ProjectileVisual, Sprite> _sprites = new();
        readonly List<SpriteRenderer> _pool = new();
        int _sortingLayer;

        public void Init(IWorld world)
        {
            _world = world;
            if (_sprites.Count == 0)
            {
                _sprites[ProjectileVisual.SwordWave] = BuildCrescent();
                _sprites[ProjectileVisual.Arrow] = BuildArrow(false);
                _sprites[ProjectileVisual.PowerArrow] = BuildArrow(true);
            }
            int id = SortingLayer.NameToID("Projectiles");   // 캐릭터보다 위
            _sortingLayer = SortingLayer.IsValid(id) ? id : 0;
        }

        void LateUpdate()
        {
            int used = 0;
            if (_world != null)
            {
                foreach (var chunk in _world.EntityManager.GetChunks(typeof(ProjectileVisualComponent), typeof(HitboxComponent), typeof(LifetimeComponent)))
                {
                    var visuals = chunk.GetComponentArray<ProjectileVisualComponent>();
                    var hitboxes = chunk.GetComponentArray<HitboxComponent>();
                    var lifetimes = chunk.GetComponentArray<LifetimeComponent>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var r = Get(used++);
                        var hb = hitboxes[i];
                        var kind = visuals[i].Kind;
                        float life = visuals[i].TotalLifetime > 0f ? Mathf.Clamp01(lifetimes[i].Remaining / visuals[i].TotalLifetime) : 1f;
                        bool wave = kind == ProjectileVisual.SwordWave;

                        r.sprite = _sprites[kind];
                        r.transform.position = new Vector3(hb.Center.X, hb.Center.Y + hb.Height + (wave ? waveHeight : arrowHeight), 0f);
                        r.transform.localScale = Vector3.one * (wave ? Mathf.Lerp(1.2f, 1f, life) : 1f);   // 검기는 날아가며 조금 커진다
                        r.flipX = hb.FacingSign < 0f;
                        r.color = new Color(1f, 1f, 1f, wave ? Mathf.Clamp01(life * 2.5f) : 1f);       // 검기만 마지막 40%에서 사라짐
                        r.sortingLayerID = _sortingLayer;
                        r.sortingOrder = Mathf.RoundToInt(-hb.Center.Y * 100f);
                    }
                }
            }
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        }

        SpriteRenderer Get(int index)
        {
            if (index < _pool.Count)
            {
                var r = _pool[index];
                if (!r.gameObject.activeSelf) r.gameObject.SetActive(true);
                return r;
            }
            var go = new GameObject("Projectile");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            _pool.Add(sr);
            return sr;
        }

        Sprite Finish(Texture2D tex, Color32[] px, string name)
        {
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            s.name = name;
            return s;
        }

        static Texture2D NewTexture(int w, int h)
            => new(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

        // 오른쪽으로 볼록한 초승달: 바깥 가장자리는 희고 안쪽으로 갈수록 하늘색으로 옅어진다
        Sprite BuildCrescent()
        {
            const int W = 48, H = 72;
            var tex = NewTexture(W, H);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W;              // 0 왼쪽 ~ 1 오른쪽
                    float v = (y + 0.5f) / H * 2f - 1f;    // -1 아래 ~ 1 위
                    float outer = Mathf.Sqrt(u * u + v * v);                       // 1 = 바깥 가장자리
                    float inner = Mathf.Sqrt((u + 0.5f) * (u + 0.5f) / 1.0f + v * v / 1.15f);
                    if (outer > 1f || inner < 1f) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }

                    float band = Mathf.Clamp01((1f - outer) / 0.5f);   // 0 바깥 ~ 1 안쪽
                    var c = Color.Lerp(edgeColor, glowColor, Mathf.SmoothStep(0f, 1f, band * 1.6f));
                    float tip = 1f - Mathf.Pow(Mathf.Abs(v), 6f);       // 위아래 끝은 가늘고 옅게
                    c.a = Mathf.Clamp01((1f - band * 0.85f) * tip);
                    px[y * W + x] = c;
                }
            }
            return Finish(tex, px, "SwordWave");
        }

        // 오른쪽을 향한 화살(깃·나무 살·쇠 촉). 강화 화살은 더 길고 하늘색 빛 테두리
        Sprite BuildArrow(bool power)
        {
            int W = power ? 40 : 22, H = power ? 9 : 5;
            int mid = H / 2;
            var tex = NewTexture(W, H);
            var px = new Color32[W * H];
            var shaft = new Color32(150, 105, 60, 255);
            var head = new Color32(205, 210, 220, 255);
            var feather = new Color32(235, 235, 225, 255);
            var glow = new Color32(120, 210, 255, 150);

            void Set(int x, int y, Color32 c) { if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = c; }

            int headLen = power ? 6 : 4;
            for (int x = 2; x < W - headLen; x++) Set(x, mid, power ? new Color32(220, 245, 255, 255) : shaft);   // 살
            for (int k = 0; k < headLen; k++)                                                                     // 촉(삼각형)
            {
                int x = W - headLen + k;
                int half = (headLen - k) / 2;
                for (int dy = -half; dy <= half; dy++) Set(x, mid + dy, head);
            }
            for (int k = 0; k < (power ? 5 : 3); k++) { Set(k, mid + 1, feather); Set(k, mid - 1, feather); }   // 깃

            if (power)
            {
                // 빛: 그려진 픽셀 둘레 한 칸을 반투명 하늘색으로
                var copy = (Color32[])px.Clone();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (copy[y * W + x].a != 0) continue;
                        bool near = false;
                        for (int dy = -1; dy <= 1 && !near; dy++)
                            for (int dx = -1; dx <= 1 && !near; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                near = nx >= 0 && nx < W && ny >= 0 && ny < H && copy[ny * W + nx].a == 255;
                            }
                        if (near) px[y * W + x] = glow;
                    }
            }
            return Finish(tex, px, power ? "PowerArrow" : "Arrow");
        }
    }
}
