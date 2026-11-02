using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 전사 필살기(도발 + 방패) 연출: 시전 순간 도발 범위만큼 주황 고리가 퍼지고,
    /// 방패 동안 발밑에 하늘색 고리가 맥박치며(끝나 갈수록 깜빡), 도발당한 적 머리 위엔 주황 "!"가 뜬다.
    /// </summary>
    public sealed class GuardRenderer : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Color guardColor = new(0.45f, 0.8f, 1f, 1f);
        [SerializeField] Color tauntColor = new(1f, 0.6f, 0.15f, 1f);
        [SerializeField] float burstTime = 0.45f;

        sealed class Fx
        {
            public GameObject Root;
            public SpriteRenderer Ring, Burst;
        }

        IWorld _world;
        Font _font;
        int _layer;
        readonly List<Fx> _guards = new();
        readonly List<TextMesh> _marks = new();

        public void Init(IWorld world)
        {
            _world = world;
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int id = SortingLayer.NameToID("Projectiles");
            _layer = SortingLayer.IsValid(id) ? id : 0;
        }

        void LateUpdate()
        {
            int guards = 0, marks = 0;
            if (_world != null)
            {
                var em = _world.EntityManager;
                float t = Time.time;
                foreach (var chunk in em.GetChunks(typeof(GuardComponent), typeof(TransformComponent)))
                {
                    var gs = chunk.GetComponentArray<GuardComponent>();
                    var trs = chunk.GetComponentArray<TransformComponent>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var g = gs[i];
                        bool active = g.Remaining > 0f;
                        bool burst = g.Elapsed < burstTime;
                        if (!active && !burst) continue;
                        DrawGuard(GetGuard(guards++), g, trs[i], active, burst, t);
                    }
                }

                foreach (var chunk in em.GetChunks(typeof(TauntedComponent), typeof(TransformComponent)))
                {
                    var ts = chunk.GetComponentArray<TauntedComponent>();
                    var trs = chunk.GetComponentArray<TransformComponent>();
                    var entities = chunk.GetEntities();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        if (ts[i].Remaining <= 0f) continue;
                        float head = em.TryGetComponent<BodyComponent>(entities[i], out var body) && body.HeadHeight > 0f ? body.HeadHeight : 1.2f;
                        var m = GetMark(marks++);
                        m.transform.position = new Vector3(trs[i].Position.X, trs[i].Position.Y + trs[i].Height + head + 0.35f, 0f);
                        float a = Mathf.Clamp01(ts[i].Remaining / 0.5f);   // 끝날 때 흐려짐
                        m.color = new Color(tauntColor.r, tauntColor.g, tauntColor.b, a);
                    }
                }
            }
            for (int i = guards; i < _guards.Count; i++) if (_guards[i].Root.activeSelf) _guards[i].Root.SetActive(false);
            for (int i = marks; i < _marks.Count; i++) if (_marks[i].gameObject.activeSelf) _marks[i].gameObject.SetActive(false);
        }

        void DrawGuard(Fx fx, in GuardComponent g, in TransformComponent tr, bool active, bool burst, float t)
        {
            fx.Root.transform.position = new Vector3(tr.Position.X, tr.Position.Y, 0f);

            fx.Ring.enabled = active;
            if (active)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f);
                bool ending = g.Remaining < 1.2f;
                float blink = ending ? (Mathf.Sin(t * 25f) > 0f ? 1f : 0.3f) : 1f;
                fx.Ring.transform.localScale = new Vector3(1.6f, 0.8f, 1f) * (1f + 0.06f * pulse);
                fx.Ring.color = new Color(guardColor.r, guardColor.g, guardColor.b, (0.55f + 0.35f * pulse) * blink);
            }

            fx.Burst.enabled = burst;
            if (burst)
            {
                float k = Mathf.Clamp01(g.Elapsed / burstTime);
                float ease = 1f - (1f - k) * (1f - k);
                var full = new Vector3(g.Radius * 2f, g.Radius, 1f) / 0.85f;   // 고리 그림은 지름의 85% 자리
                fx.Burst.transform.localScale = full * Mathf.Lerp(0.2f, 1f, ease);
                fx.Burst.color = new Color(tauntColor.r, tauntColor.g, tauntColor.b, 1f - k);
            }
        }

        Fx GetGuard(int index)
        {
            if (index < _guards.Count)
            {
                var f = _guards[index];
                if (!f.Root.activeSelf) f.Root.SetActive(true);
                return f;
            }
            var root = new GameObject("GuardFx");
            root.transform.SetParent(transform, false);
            var fx = new Fx { Root = root };
            fx.Ring = Part(root.transform, "Ring", 0, -29988);        // 바닥 바로 위, 캐릭터 뒤
            fx.Burst = Part(root.transform, "Burst", _layer, 32402);
            _guards.Add(fx);
            return fx;
        }

        TextMesh GetMark(int index)
        {
            if (index < _marks.Count)
            {
                var m = _marks[index];
                if (!m.gameObject.activeSelf) m.gameObject.SetActive(true);
                return m;
            }
            var go = new GameObject("TauntMark");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMesh>();
            text.font = _font;
            text.fontSize = 64;
            text.characterSize = 0.05f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.LowerCenter;
            text.text = "!";
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _font.material;
            mr.sortingLayerID = _layer;
            mr.sortingOrder = 32590;
            _marks.Add(text);
            return text;
        }

        static SpriteRenderer Part(Transform parent, string name, int layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = FxSprites.RingSprite;
            r.sortingLayerID = layer;
            r.sortingOrder = order;
            return r;
        }
    }
}
