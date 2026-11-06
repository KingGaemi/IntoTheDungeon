using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Unity.Bridge.View;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 맞을 때 머리 위로 피해량 숫자가 튀어 오른다(ViewBridge.AnyDamaged를 받음).
    /// 적이 맞으면 노랑, 파티가 맞으면 빨강, 큰 피해(최대 체력의 25% 이상)는 주황으로 크게.
    /// 처음에 크게 튀었다가 줄어들고, 위로 떠오르며 끝 무렵 옅어진다.
    /// </summary>
    public sealed class DamageNumberRenderer : MonoBehaviour, IWorldInjectable
    {
        [Header("모양")]
        [SerializeField] int fontSize = 64;
        [Tooltip("글자 크기(월드). 숫자 높이 ≈ fontSize × 이 값 × 0.1")]
        [SerializeField] float characterSize = 0.05f;
        [SerializeField] float bigHitScale = 1.4f;
        [Tooltip("최대 체력 대비 이 비율 이상이면 큰 피해로 표시")]
        [SerializeField] float bigHitRatio = 0.25f;

        [Header("색")]
        [SerializeField] Color enemyHitColor = new(1f, 0.92f, 0.45f, 1f);
        [SerializeField] Color bigHitColor = new(1f, 0.6f, 0.15f, 1f);
        [SerializeField] Color partyHitColor = new(1f, 0.4f, 0.35f, 1f);
        [SerializeField] Color shadowColor = new(0f, 0f, 0f, 0.85f);

        [Header("움직임")]
        [SerializeField] float lifetime = 0.8f;
        [SerializeField] float rise = 0.6f;
        [Tooltip("머리 꼭대기에서 숫자가 나오는 높이(체력바 위)")]
        [SerializeField] float startAboveHead = 0.35f;
        [Tooltip("처음 튀는 크기 배율")]
        [SerializeField] float popScale = 1.5f;

        sealed class Number
        {
            public Transform Root;
            public TextMesh Text, Shadow;
            public MeshRenderer TextRenderer, ShadowRenderer;
            public Vector3 Start;
            public float Age, Scale;
            public Color Color;
            public bool Active;
        }

        IWorld _world;
        Font _font;
        int _layer;
        readonly List<Number> _numbers = new();
        int _jitter;

        public void Init(IWorld world)
        {
            _world = world;
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int id = SortingLayer.NameToID("Projectiles");   // 캐릭터·체력바보다 위
            _layer = SortingLayer.IsValid(id) ? id : 0;
            ViewBridge.AnyDamaged -= OnDamaged;
            ViewBridge.AnyDamaged += OnDamaged;
        }

        void OnDestroy() => ViewBridge.AnyDamaged -= OnDamaged;

        void OnDamaged(DamagedEvent evt)
        {
            if (_world == null || evt.Amount <= 0) return;
            var em = _world.EntityManager;
            if (!em.Exists(evt.Victim) || !em.TryGetComponent<TransformComponent>(evt.Victim, out var t)) return;

            float head = em.TryGetComponent<BodyComponent>(evt.Victim, out var body) ? body.HeadHeight : 1.2f;
            bool party = em.TryGetComponent<TeamComponent>(evt.Victim, out var team) && team.TeamFlag == Party.Team;
            bool big = evt.Ratio >= bigHitRatio;

            // 같은 순간 여러 번 맞아도 겹치지 않게 좌우로 조금씩 흩는다
            float jx = ((_jitter++ % 5) - 2) * 0.12f;
            var n = Get();
            n.Start = new Vector3(t.Position.X + jx, t.Position.Y + t.Height + head + startAboveHead, 0f);
            n.Age = 0f;
            n.Scale = (big ? bigHitScale : 1f) * (party ? 0.85f : 1f);   // 파티 피격 숫자는 조금 작게(색은 빨강 유지)
            n.Color = party ? partyHitColor : big ? bigHitColor : enemyHitColor;
            n.Text.text = n.Shadow.text = evt.Amount.ToString();
            int order = 32600 + (_jitter % 100);   // 나중 숫자가 위에
            n.ShadowRenderer.sortingOrder = order;
            n.TextRenderer.sortingOrder = order + 1;
            n.Active = true;
            n.Root.gameObject.SetActive(true);
            Apply(n);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;   // 배속·히트스톱과 무관하게 같은 속도로(보여 주기용 연출)
            foreach (var n in _numbers)
            {
                if (!n.Active) continue;
                n.Age += dt;
                if (n.Age >= lifetime) { n.Active = false; n.Root.gameObject.SetActive(false); continue; }
                Apply(n);
            }
        }

        void Apply(Number n)
        {
            float t = Mathf.Clamp01(n.Age / lifetime);
            float up = (1f - (1f - t) * (1f - t)) * rise;                       // 빠르게 떴다가 느려짐
            float pop = n.Age < 0.12f ? Mathf.Lerp(popScale, 1f, n.Age / 0.12f) : 1f;
            float alpha = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;             // 끝 35%에서 옅어짐

            n.Root.position = n.Start + new Vector3(0f, up, 0f);
            n.Root.localScale = Vector3.one * (n.Scale * pop);
            var c = n.Color; c.a = alpha;
            n.Text.color = c;
            var s = shadowColor; s.a *= alpha;
            n.Shadow.color = s;
        }

        Number Get()
        {
            foreach (var n in _numbers) if (!n.Active) return n;

            var root = new GameObject("DamageNumber").transform;
            root.SetParent(transform, false);
            var num = new Number { Root = root };
            num.Shadow = MakeText(root, "Shadow", new Vector3(0.025f, -0.025f, 0f), out num.ShadowRenderer);
            num.Text = MakeText(root, "Text", Vector3.zero, out num.TextRenderer);
            _numbers.Add(num);
            return num;
        }

        TextMesh MakeText(Transform parent, string name, Vector3 local, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = fontSize;
            tm.characterSize = characterSize;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _font.material;
            renderer.sortingLayerID = _layer;
            return tm;
        }
    }
}
