using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Unity.Bridge.View;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 보스 연출: 그림을 BossComponent.Scale배로 키우고, 내려찍기(BossSlamComponent) 예고를 그린다.
    /// 힘을 모으는 동안 바닥에 빨간 범위 테두리 + 안쪽이 시간에 맞춰 차오르는 채움, 머리 위 "!"가 깜빡이고,
    /// 내려찍는 순간 범위만큼 주황 고리가 퍼지며 번쩍인다.
    /// </summary>
    public sealed class BossRenderer : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Color warnColor = new(1f, 0.2f, 0.15f, 1f);
        [SerializeField] Color impactColor = new(1f, 0.6f, 0.2f, 1f);
        [SerializeField] float impactTime = 0.45f;

        sealed class Fx
        {
            public Transform Root;
            public SpriteRenderer Edge, Fill, Impact, Flash;
            public TextMesh Mark;
            public MeshRenderer MarkRenderer;
        }

        IWorld _world;
        ViewBridge _views;
        Font _font;
        int _layer;
        readonly List<Fx> _pool = new();
        readonly Dictionary<Transform, Vector3> _baseScale = new();
        readonly List<IntoTheDungeon.Features.Battle.Party.Member> _party = new(8);
        int _bestStop;

        public void Init(IWorld world)
        {
            _world = world;
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int id = SortingLayer.NameToID("Projectiles");
            _layer = SortingLayer.IsValid(id) ? id : 0;
        }

        void LateUpdate()
        {
            int used = 0;
            if (_world != null)
            {
                if (!_views) _views = FindFirstObjectByType<ViewBridge>();
                var em = _world.EntityManager;
                foreach (var chunk in em.GetChunks(typeof(BossComponent)))
                {
                    var bosses = chunk.GetComponentArray<BossComponent>();
                    var entities = chunk.GetEntities();
                    for (int i = 0; i < chunk.Count; i++) ScaleView(entities[i], bosses[i].Scale);
                }

                float t = Time.time;
                _bestStop = StopPowerUi.BestPartyStop(em, _party);   // 파티가 끊을 수 있는 패턴인지(예고 표시 색)
                foreach (var chunk in em.GetChunks(typeof(BossSlamComponent), typeof(TransformComponent)))
                {
                    var slams = chunk.GetComponentArray<BossSlamComponent>();
                    var transforms = chunk.GetComponentArray<TransformComponent>();
                    var entities = chunk.GetEntities();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var s = slams[i];
                        bool warn = s.Charging && !s.Fired;
                        bool impact = s.ImpactAge >= 0f && s.ImpactAge < impactTime;
                        if (!warn && !impact) continue;
                        Draw(Get(used++), s, transforms[i], entities[i], warn, t);
                    }
                }
            }
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i].Root.gameObject.activeSelf) _pool[i].Root.gameObject.SetActive(false);
        }

        // 프리팹 원래 크기를 기억해 두고 그 배수로(방향 전환은 flipX라 크기와 안 겹친다)
        void ScaleView(Entity e, float scale)
        {
            if (!_views || !_views.TryGetGameObject(e, out var go) || !go) return;
            var tr = go.transform;
            if (!_baseScale.TryGetValue(tr, out var baseScale)) _baseScale[tr] = baseScale = tr.localScale;
            tr.localScale = baseScale * scale;
        }

        void Draw(Fx fx, in BossSlamComponent s, in TransformComponent tr, Entity e, bool warn, float t)
        {
            var center = new Vector3(s.Center.X, s.Center.Y, 0f);
            fx.Root.position = center;
            var full = new Vector3(s.Radius * 2f, s.Radius, 1f);   // 바닥 타원(깊이는 절반)
            var ring = full / 0.85f;                                 // 고리 그림은 지름의 85% 자리에 그려져 있음

            fx.Edge.enabled = fx.Fill.enabled = warn;
            if (fx.Mark.gameObject.activeSelf != warn) fx.Mark.gameObject.SetActive(warn);
            fx.Impact.enabled = fx.Flash.enabled = !warn;

            if (warn)
            {
                float k = Mathf.Clamp01(s.Timer / Mathf.Max(0.01f, s.ChargeTime));
                float blink = 0.6f + 0.4f * Mathf.Sin(t * (10f + 20f * k));   // 끝으로 갈수록 빨리 깜빡
                fx.Edge.transform.localScale = ring;
                fx.Edge.color = new Color(warnColor.r, warnColor.g, warnColor.b, 0.9f * blink);
                fx.Fill.transform.localScale = full * k;                       // 다 차는 순간 내려찍음
                fx.Fill.color = new Color(warnColor.r, warnColor.g, warnColor.b, 0.25f + 0.25f * k);

                float head = tr.Height + 2.4f;
                fx.Mark.transform.position = new Vector3(tr.Position.X, tr.Position.Y + head, 0f);
                fx.Mark.transform.localScale = Vector3.one * (1f + 0.2f * blink);
                fx.Mark.color = new Color(warnColor.r, warnColor.g * 1.5f, warnColor.b, 0.6f + 0.4f * blink);
                // "!" 아래에 요구 저지력(이 이상인 필살기 한 방이어야 끊김): 파티 최고 저지력으로 끊을 수 있으면 청록, 모자라면 빨강 + "부족"
                bool canStop = _bestStop >= s.StopRequired;
                fx.Mark.text = s.StopRequired <= 0 ? "!"
                    : $"!\n<size=40><color={(canStop ? StopPowerUi.Hex : StopPowerUi.ShortHex)}>저지 {s.StopRequired}{(canStop ? "" : " 부족")}</color></size>";
            }
            else
            {
                float k = Mathf.Clamp01(s.ImpactAge / impactTime);
                fx.Impact.transform.localScale = ring * Mathf.Lerp(0.6f, 1.2f, 1f - (1f - k) * (1f - k));
                fx.Impact.color = new Color(impactColor.r, impactColor.g, impactColor.b, 1f - k);
                fx.Flash.transform.localScale = full * 0.9f;
                fx.Flash.color = new Color(1f, 0.85f, 0.6f, 0.6f * (1f - k) * (1f - k));
            }
        }

        Fx Get(int index)
        {
            if (index < _pool.Count)
            {
                var f = _pool[index];
                if (!f.Root.gameObject.activeSelf) f.Root.gameObject.SetActive(true);
                return f;
            }
            var root = new GameObject("BossSlamFx").transform;
            root.SetParent(transform, false);
            var fx = new Fx { Root = root };
            fx.Fill = Part(root, "Fill", FxSprites.DotSprite, 0, -29990);     // 바닥 바로 위, 캐릭터 뒤
            fx.Edge = Part(root, "Edge", FxSprites.RingSprite, 0, -29989);
            fx.Flash = Part(root, "Flash", FxSprites.DotSprite, _layer, 32400);
            fx.Impact = Part(root, "Impact", FxSprites.RingSprite, _layer, 32401);

            var mark = new GameObject("Mark");
            mark.transform.SetParent(root, false);
            fx.Mark = mark.AddComponent<TextMesh>();
            fx.Mark.font = _font;
            fx.Mark.fontSize = 96;
            fx.Mark.characterSize = 0.06f;
            fx.Mark.fontStyle = FontStyle.Bold;
            fx.Mark.anchor = TextAnchor.LowerCenter;
            fx.Mark.text = "!";
            fx.Mark.alignment = TextAlignment.Center;
            fx.Mark.richText = true;
            fx.MarkRenderer = mark.GetComponent<MeshRenderer>();
            fx.MarkRenderer.sharedMaterial = _font.material;
            fx.MarkRenderer.sortingLayerID = _layer;
            fx.MarkRenderer.sortingOrder = 32600;
            _pool.Add(fx);
            return fx;
        }

        static SpriteRenderer Part(Transform parent, string name, Sprite sprite, int layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingLayerID = layer;
            r.sortingOrder = order;
            return r;
        }
    }
}
