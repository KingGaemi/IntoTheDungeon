using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Stats;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 살아 있는 캐릭터 머리 위 체력바(파티 초록, 적 빨강), 바 왼쪽에 레벨. 파티원은 바로 아래 파란 마나바도. 위치는 ECS 바닥 위치 + 높이 + 머리 높이(BodyComponent.HeadHeight).
    /// 맞으면 바는 바로 줄고, 깎인 부분은 밝은 색으로 잠깐 남았다가 천천히 따라 내려온다(얼마나 맞았는지 잘 보이게).
    /// 맞는 순간 바가 잠깐 하얗게 번쩍인다. 지도·정비 화면에선 숨긴다.
    /// </summary>
    public sealed class HealthBarRenderer : MonoBehaviour, IWorldInjectable
    {
        [Header("크기(월드 단위)")]
        [SerializeField] float width = 1.1f;
        [SerializeField] float height = 0.14f;
        [SerializeField] float border = 0.025f;
        [Tooltip("머리 꼭대기에서 바까지 띄우는 높이")]
        [SerializeField] float gapAboveHead = 0.15f;

        [Header("색")]
        [SerializeField] Color partyColor = new(0.35f, 0.85f, 0.4f, 1f);
        [SerializeField] Color enemyColor = new(0.88f, 0.26f, 0.22f, 1f);
        [Tooltip("중독 중(독꽃)")]
        [SerializeField] Color poisonColor = new(0.62f, 0.35f, 0.85f, 1f);
        [SerializeField] Color trailColor = new(1f, 0.93f, 0.6f, 1f);
        [SerializeField] Color backColor = new(0.08f, 0.08f, 0.1f, 0.85f);
        [Tooltip("파티원 체력바 아래 마나바")]
        [SerializeField] Color manaColor = new(0.3f, 0.55f, 1f, 1f);
        [SerializeField] float manaHeight = 0.06f;
        [SerializeField] Color borderColor = new(0f, 0f, 0f, 0.9f);
        [Header("기본 스킬 재사용 대기(체력바 오른쪽 동그라미)")]
        [SerializeField] int cooldownDots = 12;
        [SerializeField] float cooldownRadius = 0.11f;
        [SerializeField] float cooldownDotSize = 0.05f;
        [SerializeField] Color cooldownReady = new(0.55f, 0.95f, 1f, 1f);
        [SerializeField] Color cooldownCharging = new(0.3f, 0.6f, 0.8f, 1f);
        [SerializeField] Color cooldownEmpty = new(0.08f, 0.08f, 0.1f, 0.85f);

        [Header("깎이는 연출")]
        [Tooltip("맞은 뒤 잔상이 버티는 시간(초)")]
        [SerializeField] float trailHold = 0.35f;
        [Tooltip("잔상이 초당 줄어드는 비율(최대 체력 대비)")]
        [SerializeField] float trailSpeed = 0.8f;
        [SerializeField] float flashTime = 0.08f;

        [Header("칸 눈금(최대 체력 스케일)")]
        [Tooltip("눈금 하나 = 체력 이만큼. 바 폭은 같으니 최대 체력이 클수록 칸이 촘촘해진다")]
        [SerializeField] int tickUnit = 10;
        [Tooltip("이 칸마다 굵고 긴 눈금")]
        [SerializeField] int majorEvery = 5;
        [Tooltip("눈금이 이보다 많아지면 단위를 10배로 키운다(너무 빽빽하지 않게)")]
        [SerializeField] int maxTicks = 40;
        [SerializeField] float tickWidth = 0.016f;
        [SerializeField] Color tickColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] Color majorTickColor = new(0f, 0f, 0f, 0.9f);

        sealed class Bar
        {
            public Transform Root;
            public SpriteRenderer Border, Back, Trail, Fill;
            public SpriteRenderer ManaBorder, ManaBack, ManaFill;   // 파티원만 켬
            public TextMesh Level, LevelShadow;                      // 바 왼쪽 "Lv.3"
            public SpriteRenderer CdBack;                            // 기본 스킬 재사용 대기(동그라미)
            public readonly List<SpriteRenderer> CdDots = new();
            public MeshRenderer LevelRenderer, LevelShadowRenderer;
            public int ShownLevel = -1;
            public float Shown = -1f, Trail_ = 1f, HoldUntil, FlashUntil;
            public bool Used;
            public readonly List<SpriteRenderer> Ticks = new();
            public int TickMaxHp = -1;   // 눈금을 만든 기준 최대 체력(바뀌면 다시 긋는다)
        }

        IWorld _world;
        BattleProgress _progress;
        Sprite _left, _center;
        Font _font;
        int _layer;
        readonly Dictionary<Entity, Bar> _bars = new();
        readonly List<Entity> _stale = new();

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _progress);
            if (!_left)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var white = (Color32)Color.white;
                tex.SetPixels32(new[] { white, white, white, white });
                tex.Apply();
                _left = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0f, 0.5f), 2f);     // 왼쪽 기준(채움 비율로 늘림)
                _center = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            }
            int id = SortingLayer.NameToID("Projectiles");   // 캐릭터보다 위
            _layer = SortingLayer.IsValid(id) ? id : 0;
        }

        void LateUpdate()
        {
            foreach (var b in _bars.Values) b.Used = false;
            bool hidden = _world == null || (_progress != null && _progress.Phase == BattlePhase.Map);

            if (!hidden)
            {
                float now = Time.unscaledTime;   // 깎인 체력 꼬리는 배속과 무관하게
                bool snap = _progress != null && _progress.Phase is BattlePhase.Intro or BattlePhase.Deploy;
                foreach (var chunk in _world.EntityManager.GetChunks(typeof(StatusComponent), typeof(TransformComponent), typeof(TeamComponent)))
                {
                    var statuses = chunk.GetComponentArray<StatusComponent>();
                    var transforms = chunk.GetComponentArray<TransformComponent>();
                    var teams = chunk.GetComponentArray<TeamComponent>();
                    var entities = chunk.GetEntities();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var st = statuses[i];
                        if (!st.IsAlive || st.MaxHp <= 0) continue;
                        var e = entities[i];

                        if (!_bars.TryGetValue(e, out var bar)) { bar = CreateBar(); _bars[e] = bar; }
                        bar.Used = true;

                        float ratio = Mathf.Clamp01((float)st.CurrentHp / st.MaxHp);
                        // 처음 보일 때와 전투 시작 연출 중(이전 방 체력·유물 효과를 입히는 때)은 맞은 게 아니므로 잔상 없이 바로
                        if (bar.Shown < 0f || snap) { bar.Shown = ratio; bar.Trail_ = ratio; bar.FlashUntil = 0f; }
                        if (ratio < bar.Shown - 1e-4f) { bar.HoldUntil = now + trailHold; bar.FlashUntil = now + flashTime; }
                        bar.Shown = ratio;
                        if (bar.Trail_ < ratio) bar.Trail_ = ratio;                          // 회복은 바로
                        else if (now >= bar.HoldUntil) bar.Trail_ = Mathf.MoveTowards(bar.Trail_, ratio, trailSpeed * Time.unscaledDeltaTime);

                        float head = _world.EntityManager.TryGetComponent<BodyComponent>(e, out var body) ? body.HeadHeight : 1.2f;
                        var t = transforms[i];
                        bar.Root.position = new Vector3(t.Position.X, t.Position.Y + t.Height + head + gapAboveHead, 0f);

                        bool party = teams[i].TeamFlag == Party.Team;
                        // 마나바: 파티원만(몹은 마나를 안 씀)
                        DerivedStatsComponent ds = default;
                        bool showMana = party && _world.EntityManager.TryGetComponent(e, out ds) && ds.MaxMana > 0f;
                        if (bar.ManaFill.enabled != showMana) bar.ManaBorder.enabled = bar.ManaBack.enabled = bar.ManaFill.enabled = showMana;
                        if (showMana) SetWidth(bar.ManaFill, Mathf.Clamp01(ds.Mana / ds.MaxMana));
                        // 기본 스킬 재사용 대기: 12시 방향부터 시계 방향으로 점이 차오르고, 다 차면 깜빡, 쓰는 중엔 흰색
                        bool hasBasic = _world.EntityManager.TryGetComponent<BasicSkillComponent>(e, out var basic) && basic.Skill.Cooldown > 0f;
                        if (bar.CdBack.enabled != hasBasic) { bar.CdBack.enabled = hasBasic; foreach (var d in bar.CdDots) d.enabled = hasBasic; }
                        if (hasBasic)
                        {
                            var bs = basic.Skill;
                            float k = 1f - Mathf.Clamp01(bs.CooldownRemaining / bs.Cooldown);
                            bool ready = bs.CooldownRemaining <= 0f;
                            float pulse = 0.65f + 0.35f * Mathf.Sin(now * 8f);
                            for (int d = 0; d < bar.CdDots.Count; d++)
                            {
                                bool lit = ready || (d + 1f) / bar.CdDots.Count <= k + 1e-4f;
                                var c = bs.Casting ? Color.white : ready ? cooldownReady * new Color(pulse, pulse, pulse, 1f) : lit ? cooldownCharging : cooldownEmpty;
                                bar.CdDots[d].color = c;
                            }
                        }
                        bool poisoned = _world.EntityManager.TryGetComponent<PoisonComponent>(e, out var poison) && poison.Active;
                        bar.Fill.color = now < bar.FlashUntil ? Color.white : poisoned ? poisonColor : party ? partyColor : enemyColor;
                        SetWidth(bar.Fill, ratio);
                        SetWidth(bar.Trail, bar.Trail_);

                        // 앞에 있는(깊이가 작은) 캐릭터의 바가 위에 그려지게
                        int order = 24000 + Mathf.Clamp(Mathf.RoundToInt(-t.Position.Y * 10f), -1500, 1500) * 5;   // 16500~31504(short 범위 안)
                        bar.Border.sortingOrder = order;
                        bar.Back.sortingOrder = order + 1;
                        bar.Trail.sortingOrder = order + 2;
                        bar.Fill.sortingOrder = order + 3;
                        bar.ManaBorder.sortingOrder = order;
                        bar.ManaBack.sortingOrder = order + 1;
                        bar.ManaFill.sortingOrder = order + 3;
                        bar.CdBack.sortingOrder = order;
                        foreach (var d in bar.CdDots) d.sortingOrder = order + 3;

                        // 레벨(바 왼쪽)
                        int level = _world.EntityManager.TryGetComponent<PrimaryStatsComponent>(e, out var ps) ? ps.Level : 0;
                        if (bar.ShownLevel != level)
                        {
                            bar.ShownLevel = level;
                            bar.Level.text = bar.LevelShadow.text = level > 0 ? $"Lv.{level}" : "";
                        }
                        bar.LevelShadowRenderer.sortingOrder = order + 3;
                        bar.LevelRenderer.sortingOrder = order + 4;
                        if (bar.TickMaxHp != st.MaxHp) BuildTicks(bar, st.MaxHp);
                        foreach (var tick in bar.Ticks) tick.sortingOrder = order + 4;   // 채움 위
                    }
                }
            }

            // 이번 프레임에 안 쓴 바(죽었거나 사라진 캐릭터)는 숨기고 정리
            _stale.Clear();
            foreach (var kv in _bars)
            {
                if (kv.Value.Used) { if (!kv.Value.Root.gameObject.activeSelf) kv.Value.Root.gameObject.SetActive(true); continue; }
                if (hidden) { kv.Value.Root.gameObject.SetActive(false); continue; }
                Destroy(kv.Value.Root.gameObject);
                _stale.Add(kv.Key);
            }
            foreach (var e in _stale) _bars.Remove(e);
        }

        void SetWidth(SpriteRenderer r, float ratio)
        {
            var s = r.transform.localScale;
            s.x = Mathf.Max(0f, width * ratio);
            r.transform.localScale = s;
        }

        // 롤·도타처럼 일정 체력마다 눈금: 짧은 눈금은 위에서 아래로 60%, majorEvery칸마다 끝까지 굵게
        void BuildTicks(Bar bar, int maxHp)
        {
            foreach (var t in bar.Ticks) Destroy(t.gameObject);
            bar.Ticks.Clear();
            bar.TickMaxHp = maxHp;

            int unit = Mathf.Max(1, tickUnit);
            while (maxHp / unit > maxTicks) unit *= 10;
            for (int hp = unit, k = 1; hp < maxHp; hp += unit, k++)
            {
                bool major = majorEvery > 0 && k % majorEvery == 0;
                float x = -width * 0.5f + width * hp / maxHp;
                float h = major ? height : height * 0.6f;
                var tick = Part(bar.Root, major ? "TickMajor" : "Tick", _center, major ? majorTickColor : tickColor,
                                new Vector3(x, (height - h) * 0.5f), new Vector2(major ? tickWidth * 1.6f : tickWidth, h));
                bar.Ticks.Add(tick);
            }
        }

        Bar CreateBar()
        {
            var root = new GameObject("HpBar").transform;
            root.SetParent(transform, false);
            var bar = new Bar { Root = root };
            bar.Border = Part(root, "Border", _center, borderColor, new Vector3(0f, 0f), new Vector2(width + border * 2f, height + border * 2f));
            bar.Back = Part(root, "Back", _center, backColor, new Vector3(0f, 0f), new Vector2(width, height));
            bar.Trail = Part(root, "Trail", _left, trailColor, new Vector3(-width * 0.5f, 0f), new Vector2(width, height));
            bar.Fill = Part(root, "Fill", _left, partyColor, new Vector3(-width * 0.5f, 0f), new Vector2(width, height));

            // 마나바: 체력바 바로 아래(테두리 한 줄 공유)
            float my = -(height + manaHeight) * 0.5f - border;
            bar.ManaBorder = Part(root, "ManaBorder", _center, borderColor, new Vector3(0f, my), new Vector2(width + border * 2f, manaHeight + border * 2f));
            bar.ManaBack = Part(root, "ManaBack", _center, backColor, new Vector3(0f, my), new Vector2(width, manaHeight));
            bar.ManaFill = Part(root, "ManaFill", _left, manaColor, new Vector3(-width * 0.5f, my), new Vector2(width, manaHeight));
            bar.ManaBorder.enabled = bar.ManaBack.enabled = bar.ManaFill.enabled = false;

            // 기본 스킬 재사용 대기: 체력바 오른쪽 끝 옆의 동그라미(점 12개)
            var cdCenter = new Vector3(width * 0.5f + border + cooldownRadius + cooldownDotSize, -manaHeight * 0.5f, 0f);
            bar.CdBack = Part(root, "CdBack", FxSprites.DotSprite, new Color(0f, 0f, 0f, 0.75f), cdCenter,
                              Vector2.one * (cooldownRadius + cooldownDotSize) * 2.2f);
            for (int d = 0; d < cooldownDots; d++)
            {
                float a = Mathf.PI * 0.5f - d * Mathf.PI * 2f / cooldownDots;   // 12시부터 시계 방향
                var pos = cdCenter + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * cooldownRadius;
                bar.CdDots.Add(Part(root, "CdDot", FxSprites.DotSprite, cooldownEmpty, pos, Vector2.one * cooldownDotSize));
            }
            bar.CdBack.enabled = false;
            foreach (var d in bar.CdDots) d.enabled = false;

            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var levelPos = new Vector3(-width * 0.5f - border - 0.04f, 0f, 0f);
            bar.LevelShadow = LevelText(root, "LevelShadow", levelPos + new Vector3(0.015f, -0.015f, 0f), new Color(0f, 0f, 0f, 0.9f), out bar.LevelShadowRenderer);
            bar.Level = LevelText(root, "Level", levelPos, new Color(1f, 0.92f, 0.6f, 1f), out bar.LevelRenderer);
            return bar;
        }

        TextMesh LevelText(Transform parent, string name, Vector3 local, Color color, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 48;
            tm.characterSize = 0.035f;   // 글자 높이 ≈ 바 두께 정도
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleRight;
            tm.color = color;
            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _font.material;
            renderer.sortingLayerID = _layer;
            return tm;
        }

        SpriteRenderer Part(Transform parent, string name, Sprite sprite, Color color, Vector3 local, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);   // 스프라이트 1유닛 → 크기만큼
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingLayerID = _layer;
            return r;
        }
    }
}
