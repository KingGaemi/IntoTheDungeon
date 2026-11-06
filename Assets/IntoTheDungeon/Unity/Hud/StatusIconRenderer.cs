using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 상태 표시(월드 스프라이트): 머리 위 체력바 오른쪽에 상태 아이콘 줄(보호막·독·방패·광폭, 최대 4. 아이콘 아래 1px 게이지, 광폭은 ×N),
    /// 몸에는 보호막 파란 타원 고리(호흡, 끝나기 1초 전 깜빡)·광폭 발밑 주황 불씨(초당 1+스택)·보스 발밑 자주 오라.
    /// HealthBarRenderer의 바 자리·정렬 공식(b = 24000 + clamp(round(−Y×10), ±1500)×5)을 따라 b+7~9에 그린다(겹치지 않게).
    /// 파티원도 같은 규칙(방패·독). 보스는 1.4배. 지도·정비 화면에선 숨김. 연출은 unscaled 시간.
    /// </summary>
    [AutoHud]
    public sealed class StatusIconRenderer : MonoBehaviour, IWorldInjectable
    {
        // HealthBarRenderer 기본값과 같게(임시값): 바 폭·테두리·머리 위 간격. 파티원은 바 오른쪽에 기본 스킬 동그라미가 있어 그 뒤부터
        const float BarWidth = 1.1f, BarBorder = 0.025f, GapAboveHead = 0.15f;
        const float CdCircleCenter = BarWidth * 0.5f + BarBorder + 0.11f + 0.05f, CdCircleReach = (0.11f + 0.05f) * 1.1f;
        const float IconSize = 0.2f, IconGap = 0.22f, IconStart = 0.08f, GaugeH = 0.03f;   // (임시값)
        const int MaxIcons = 4, MaxEmbers = 64;
        const float BossScale = 1.4f;

        static readonly Color BarrierColor = new(0.4f, 0.65f, 1f, 1f);
        static readonly Color GuardColor = new(0.45f, 0.8f, 1f, 1f);
        static readonly Color EnrageColor = new(1f, 0.6f, 0.15f, 1f);
        static readonly Color AuraColor = new(0.45f, 0.2f, 0.5f, 1f);

        sealed class Icon
        {
            public Transform Root;
            public SpriteRenderer Img, GaugeBack, GaugeFill;
            public TextMesh Count;
            public MeshRenderer CountRenderer;
            public string Id;
            public int ShownCount = -1;
        }

        sealed class Row
        {
            public Transform Root;                          // 발 위치
            public readonly Icon[] Icons = new Icon[MaxIcons];
            public SpriteRenderer BarrierRing, Aura;        // 몸 표시
            public bool Used;
            public float PoisonMax = -1f;                   // 독 게이지 기준(최초 관찰값, 더 큰 값이 오면 갱신)
            public float EmberAcc;                          // 불씨 누적(초당 1 + 스택)
        }

        sealed class Ember
        {
            public SpriteRenderer R;
            public float Age, Life;
            public Vector3 Pos, Vel;
            public bool Active;
        }

        struct Entry { public string Id; public float Gauge; public int Count; public Color Color; }   // Gauge < 0 → 게이지 없음

        IWorld _world;
        BattleProgress _progress;
        Font _font;
        int _layer;
        Sprite _left, _center;
        readonly Dictionary<Entity, Row> _rows = new();
        readonly List<Entity> _stale = new();
        readonly List<Ember> _embers = new(MaxEmbers);
        readonly Entry[] _entries = new Entry[MaxIcons];
        readonly System.Random _rng = new(7);   // 불씨 흩뿌리기(연출 전용 — 판 난수 RunData.Rng와 분리해 결정성에 영향 없음)

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _progress);
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!_left)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var white = (Color32)Color.white;
                tex.SetPixels32(new[] { white, white, white, white });
                tex.Apply();
                _left = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0f, 0.5f), 2f);     // 왼쪽 기준(게이지)
                _center = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            }
            int id = SortingLayer.NameToID("Projectiles");   // 체력바와 같은 레이어(캐릭터보다 위)
            _layer = SortingLayer.IsValid(id) ? id : 0;
        }

        void LateUpdate()
        {
            foreach (var r in _rows.Values) r.Used = false;
            bool hidden = _world == null || (_progress != null && _progress.Phase == BattlePhase.Map);
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;

            if (!hidden)
            {
                var em = _world.EntityManager;
                foreach (var chunk in em.GetChunks(typeof(StatusComponent), typeof(TransformComponent), typeof(TeamComponent)))
                {
                    var statuses = chunk.GetComponentArray<StatusComponent>();
                    var transforms = chunk.GetComponentArray<TransformComponent>();
                    var teams = chunk.GetComponentArray<TeamComponent>();
                    var entities = chunk.GetEntities();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        if (!statuses[i].IsAlive) continue;
                        var e = entities[i];
                        bool boss = em.HasComponent<BossComponent>(e);
                        bool barrierOn = em.TryGetComponent<BarrierComponent>(e, out var barrier) && barrier.Remaining > 0f;
                        bool poisoned = em.TryGetComponent<PoisonComponent>(e, out var poison) && poison.Active;
                        bool guarded = em.TryGetComponent<GuardComponent>(e, out var guard) && guard.Remaining > 0f;
                        int stacks = em.TryGetComponent<EnrageComponent>(e, out var enrage) ? enrage.Stacks : 0;   // patterns-boss가 채움(없으면 0)
                        if (!barrierOn && !poisoned && !guarded && stacks < 1 && !boss) continue;   // 보일 게 없으면 줄도 안 만든다

                        if (!_rows.TryGetValue(e, out var row)) { row = CreateRow(); _rows[e] = row; }
                        row.Used = true;

                        // 아이콘 줄 내용(순서: 보호막·독·방패·광폭)
                        int n = 0;
                        if (barrierOn) _entries[n++] = new Entry { Id = StatusIcons.Barrier, Gauge = barrier.Duration > 0f ? Mathf.Clamp01(barrier.Remaining / barrier.Duration) : 1f, Color = BarrierColor };
                        if (poisoned)
                        {
                            if (row.PoisonMax < poison.Remaining) row.PoisonMax = poison.Remaining;
                            _entries[n++] = new Entry { Id = StatusIcons.Poison, Gauge = row.PoisonMax > 0f ? Mathf.Clamp01(poison.Remaining / row.PoisonMax) : 1f, Color = UiKit.Poison };
                        }
                        else row.PoisonMax = -1f;
                        if (guarded && n < MaxIcons) _entries[n++] = new Entry { Id = StatusIcons.Guard, Gauge = guard.Duration > 0f ? Mathf.Clamp01(guard.Remaining / guard.Duration) : 1f, Color = GuardColor };
                        if (stacks >= 1 && n < MaxIcons) _entries[n++] = new Entry { Id = StatusIcons.Enrage, Gauge = -1f, Count = stacks, Color = EnrageColor };

                        var t = transforms[i];
                        bool hasBody = em.TryGetComponent<BodyComponent>(e, out var body);
                        float head = hasBody ? body.HeadHeight : 1.2f;
                        float radius = hasBody && body.Radius > 0f ? body.Radius : 0.4f;
                        row.Root.position = new Vector3(t.Position.X, t.Position.Y, 0f);
                        float barY = t.Height + head + GapAboveHead;   // 발 기준 체력바 높이(보스는 바가 숨겨져도 같은 자리)
                        float scale = boss ? BossScale : 1f;
                        bool party = teams[i].TeamFlag == Party.Team;
                        bool hasCircle = party && em.TryGetComponent<BasicSkillComponent>(e, out var basic) && basic.Skill.Cooldown > 0f;
                        float x0 = (hasCircle ? CdCircleCenter + CdCircleReach : BarWidth * 0.5f + BarBorder) + IconStart;
                        int order = 24000 + Mathf.Clamp(Mathf.RoundToInt(-t.Position.Y * 10f), -1500, 1500) * 5;   // HealthBarRenderer와 같은 공식
                        int bodyOrder = Mathf.Clamp(Mathf.RoundToInt(-t.Position.Y * 100f), short.MinValue + 4, short.MaxValue - 1);   // ViewBridge 캐릭터 정렬

                        for (int k = 0; k < MaxIcons; k++)
                        {
                            var ic = row.Icons[k];
                            bool on = k < n;
                            if (ic.Root.gameObject.activeSelf != on) ic.Root.gameObject.SetActive(on);
                            if (!on) continue;
                            var en = _entries[k];
                            ic.Root.localPosition = new Vector3(x0 + (k * IconGap + IconSize * 0.5f) * scale, barY, 0f);
                            ic.Root.localScale = new Vector3(scale, scale, 1f);
                            if (ic.Id != en.Id) { ic.Id = en.Id; ic.Img.sprite = StatusIcons.World(en.Id); }
                            ic.Img.sortingOrder = order + 7;
                            bool gauge = en.Gauge >= 0f;
                            if (ic.GaugeBack.enabled != gauge) ic.GaugeBack.enabled = ic.GaugeFill.enabled = gauge;
                            if (gauge)
                            {
                                ic.GaugeFill.color = en.Color;
                                var s = ic.GaugeFill.transform.localScale;
                                s.x = IconSize * en.Gauge;
                                ic.GaugeFill.transform.localScale = s;
                            }
                            ic.GaugeBack.sortingOrder = order + 8;
                            ic.GaugeFill.sortingOrder = order + 9;
                            bool count = en.Count >= 2;   // ×N은 2부터
                            if (ic.Count.gameObject.activeSelf != count) ic.Count.gameObject.SetActive(count);
                            if (count)
                            {
                                if (ic.ShownCount != en.Count) { ic.ShownCount = en.Count; ic.Count.text = "×" + en.Count; }
                                ic.Count.color = en.Color;
                                ic.CountRenderer.sortingOrder = order + 9;
                            }
                        }

                        // 보호막: 파란 타원 고리(호흡 ±4%, 남은 1초는 6Hz 깜빡), 캐릭터 바로 뒤
                        if (row.BarrierRing.enabled != barrierOn) row.BarrierRing.enabled = barrierOn;
                        if (barrierOn)
                        {
                            float breath = 1f + 0.04f * Mathf.Sin(now * 4f);
                            float blink = barrier.Remaining < 1f ? (Mathf.Sin(now * Mathf.PI * 12f) > 0f ? 1f : 0.35f) : 1f;
                            row.BarrierRing.transform.localScale = new Vector3(radius * 2.6f, radius * 1.3f, 1f) * breath;
                            row.BarrierRing.color = new Color(BarrierColor.r, BarrierColor.g, BarrierColor.b, 0.55f * blink);
                            row.BarrierRing.sortingOrder = bodyOrder - 1;
                        }

                        // 보스: 발밑 자주 오라(호흡), 보호막 고리보다 한 칸 뒤
                        if (row.Aura.enabled != boss) row.Aura.enabled = boss;
                        if (boss)
                        {
                            float breath = 1f + 0.06f * Mathf.Sin(now * 2.5f);
                            row.Aura.transform.localScale = new Vector3(radius * 3.2f, radius * 1.6f, 1f) * breath;   // (임시값)
                            row.Aura.color = new Color(AuraColor.r, AuraColor.g, AuraColor.b, 0.25f + 0.05f * Mathf.Sin(now * 2.5f));
                            row.Aura.sortingOrder = bodyOrder - 2;
                        }

                        // 광폭: 발밑 주황 불씨 초당 1 + 스택
                        if (stacks >= 1)
                        {
                            row.EmberAcc += dt * (1f + stacks);
                            while (row.EmberAcc >= 1f) { row.EmberAcc -= 1f; SpawnEmber(row.Root.position, radius, bodyOrder + 1); }
                        }
                        else row.EmberAcc = 0f;
                    }
                }
            }

            // 불씨: 위로 떠오르며 작아지고 옅어진다
            for (int i = 0; i < _embers.Count; i++)
            {
                var eb = _embers[i];
                if (!eb.Active) continue;
                eb.Age += dt;
                float p = eb.Life > 0f ? eb.Age / eb.Life : 1f;
                if (hidden || p >= 1f) { eb.Active = false; eb.R.enabled = false; continue; }
                eb.Pos += eb.Vel * dt;
                eb.Vel.x *= Mathf.Max(0f, 1f - 2f * dt);
                eb.R.transform.position = eb.Pos;
                float size = Mathf.Lerp(0.09f, 0.03f, p);
                eb.R.transform.localScale = new Vector3(size, size, 1f);
                eb.R.color = new Color(1f, Mathf.Lerp(0.75f, 0.35f, p), 0.15f, 1f - p * p);
            }

            // 이번 프레임에 안 쓴 줄(죽었거나 상태가 끝난 캐릭터)은 숨기고 정리
            _stale.Clear();
            foreach (var kv in _rows)
            {
                if (kv.Value.Used) { if (!kv.Value.Root.gameObject.activeSelf) kv.Value.Root.gameObject.SetActive(true); continue; }
                if (hidden) { kv.Value.Root.gameObject.SetActive(false); continue; }
                Destroy(kv.Value.Root.gameObject);
                _stale.Add(kv.Key);
            }
            foreach (var e in _stale) _rows.Remove(e);
        }

        void SpawnEmber(Vector3 feet, float radius, int order)
        {
            Ember em = null;
            foreach (var x in _embers) if (!x.Active) { em = x; break; }
            if (em == null)
            {
                if (_embers.Count >= MaxEmbers) return;   // 가득 차면 이번 불씨는 건너뛴다
                var r = Part(transform, "Ember", FxSprites.DotSprite, EnrageColor, Vector3.zero, Vector2.one * 0.08f, 0);
                em = new Ember { R = r };
                _embers.Add(em);
            }
            float ox = ((float)_rng.NextDouble() * 2f - 1f) * radius * 0.9f;
            em.Pos = feet + new Vector3(ox, (float)_rng.NextDouble() * 0.1f, 0f);
            em.Vel = new Vector3(((float)_rng.NextDouble() - 0.5f) * 0.6f, 0.6f + (float)_rng.NextDouble() * 0.5f, 0f);
            em.Age = 0f;
            em.Life = 0.6f + (float)_rng.NextDouble() * 0.3f;
            em.Active = true;
            em.R.enabled = true;
            em.R.sortingOrder = order;
            em.R.transform.position = em.Pos;
        }

        Row CreateRow()
        {
            var root = new GameObject("StatusRow").transform;
            root.SetParent(transform, false);
            var row = new Row { Root = root };
            row.BarrierRing = Part(root, "BarrierRing", FxSprites.RingSprite, BarrierColor, Vector3.zero, Vector2.one, 0);   // Default 레이어(캐릭터와 같은 정렬 축)
            row.Aura = Part(root, "BossAura", FxSprites.RingSprite, AuraColor, Vector3.zero, Vector2.one, 0);
            row.BarrierRing.enabled = row.Aura.enabled = false;
            for (int k = 0; k < MaxIcons; k++) row.Icons[k] = CreateIcon(root);
            return row;
        }

        Icon CreateIcon(Transform parent)
        {
            var root = new GameObject("Status").transform;
            root.SetParent(parent, false);
            var ic = new Icon { Root = root };
            ic.Img = Part(root, "Icon", null, Color.white, Vector3.zero, Vector2.one, _layer);   // 스프라이트가 0.2유닛(ppu 80)
            float gy = -IconSize * 0.5f - GaugeH * 0.5f - 0.01f;
            ic.GaugeBack = Part(root, "GaugeBack", _center, new Color(0f, 0f, 0f, 0.75f), new Vector3(0f, gy, 0f), new Vector2(IconSize + 0.02f, GaugeH + 0.02f), _layer);
            ic.GaugeFill = Part(root, "GaugeFill", _left, Color.white, new Vector3(-IconSize * 0.5f, gy, 0f), new Vector2(IconSize, GaugeH), _layer);
            ic.GaugeBack.enabled = ic.GaugeFill.enabled = false;

            // ×N(아이콘 오른쪽)
            var go = new GameObject("Count");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(IconSize * 0.5f + 0.02f, 0f, 0f);
            ic.Count = go.AddComponent<TextMesh>();
            ic.Count.font = _font;
            ic.Count.fontSize = 48;
            ic.Count.characterSize = 0.035f;
            ic.Count.fontStyle = FontStyle.Bold;
            ic.Count.anchor = TextAnchor.MiddleLeft;
            ic.Count.text = "";
            ic.CountRenderer = go.GetComponent<MeshRenderer>();
            ic.CountRenderer.sharedMaterial = _font.material;
            ic.CountRenderer.sortingLayerID = _layer;
            go.SetActive(false);
            root.gameObject.SetActive(false);
            return ic;
        }

        static SpriteRenderer Part(Transform parent, string name, Sprite sprite, Color color, Vector3 local, Vector2 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingLayerID = layer;
            return r;
        }
    }
}
