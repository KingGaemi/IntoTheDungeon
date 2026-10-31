using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Attack;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 검기(SwordWaveComponent + HitboxComponent) 엔티티를 매 프레임 읽어 코드로 그린 초승달 스프라이트로 보여준다.
    /// 판정 박스 위치를 그대로 따라가고(화면 y = 깊이 + 높이), 수명 끝 무렵 옅어진다.
    /// 진짜 이펙트 그림이 생기면 이 그림만 바꾸면 된다.
    /// </summary>
    public sealed class SwordWaveRenderer : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] int pixelsPerUnit = 40;
        [Tooltip("판정 높이(발)에서 그림 중심까지 올리는 높이(가슴 높이쯤)")]
        [SerializeField] float bodyHeight = 0.7f;
        [SerializeField] Color edgeColor = new(1f, 1f, 1f, 1f);
        [SerializeField] Color glowColor = new(0.35f, 0.8f, 1f, 1f);

        IWorld _world;
        Sprite _sprite;
        readonly List<SpriteRenderer> _pool = new();
        int _sortingLayer;

        public void Init(IWorld world)
        {
            _world = world;
            if (!_sprite) _sprite = BuildSprite();
            int id = SortingLayer.NameToID("Projectiles");   // 캐릭터보다 위
            _sortingLayer = SortingLayer.IsValid(id) ? id : 0;
        }

        void LateUpdate()
        {
            int used = 0;
            if (_world != null)
            {
                foreach (var chunk in _world.EntityManager.GetChunks(typeof(SwordWaveComponent), typeof(HitboxComponent), typeof(LifetimeComponent)))
                {
                    var waves = chunk.GetComponentArray<SwordWaveComponent>();
                    var hitboxes = chunk.GetComponentArray<HitboxComponent>();
                    var lifetimes = chunk.GetComponentArray<LifetimeComponent>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var r = Get(used++);
                        var hb = hitboxes[i];
                        float life = waves[i].TotalLifetime > 0f ? Mathf.Clamp01(lifetimes[i].Remaining / waves[i].TotalLifetime) : 1f;

                        r.transform.position = new Vector3(hb.Center.X, hb.Center.Y + hb.Height + bodyHeight, 0f);
                        r.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, life);   // 날아가며 조금 커진다
                        r.flipX = hb.FacingSign < 0f;
                        r.color = new Color(1f, 1f, 1f, Mathf.Clamp01(life * 2.5f));          // 마지막 40%에서 사라짐
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
            var go = new GameObject("SwordWave");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            _pool.Add(sr);
            return sr;
        }

        // 오른쪽으로 볼록한 초승달: 바깥 가장자리는 희고 안쪽으로 갈수록 하늘색으로 옅어진다
        Sprite BuildSprite()
        {
            const int W = 48, H = 72;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
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
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            s.name = "SwordWave";
            return s;
        }
    }
}
