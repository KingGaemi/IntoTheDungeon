using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Unity.Bridge.View;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 타격 피드백: 맞은 쪽 스쿼시(파티원은 약하게), 적만 실루엣 플래시 + 노란 스파크, 막타는 더 크게,
    /// 넉백으로 밀리기 시작할 때 발밑 먼지, 투사체가 사라진 자리의 파편(ProjectileRenderer가 알려 줌).
    /// ViewBridge.AnyDamaged를 받고 전부 실제 시간으로 논다. 프레임당 스파크 상한 MaxSparksPerFrame, 배속 1.5 초과면 절반.
    /// 파티 피격엔 플래시·스파크 없음(부정 신호는 빨간 숫자뿐).
    /// </summary>
    [AutoHud]
    public sealed class HitFxRenderer : MonoBehaviour, IWorldInjectable
    {
        const int MaxSparksPerFrame = 24, MaxParticles = 160;
        const float Sensitivity = 9f;                   // HitShake와 같은 피해 비율 가중
        const float BigHitRatio = 0.25f;                // 이 비율 이상이면 플래시가 조금 길다
        const float KbDustFrom = 0.5f, KbDustTo = 2f;   // 넉백 속도가 이 미만 → 이 이상으로 뛰면 먼지 (임시값)
        const int SparkOrder = 32500;                   // 데미지 숫자(32600) 아래, 체력바 위

        static readonly Color SparkColor = new(1f, 0.92f, 0.45f, 1f);
        static readonly Color DustColor = new(0.62f, 0.6f, 0.56f, 0.75f);
        static readonly Color WaveFrag = new(0.75f, 1f, 1f, 1f);      // 검기: 청록-흰
        static readonly Color ArrowFrag = new(0.6f, 0.42f, 0.25f, 1f); // 화살: 갈색
        static readonly Color OrbFrag = new(0.5f, 1f, 0.6f, 1f);       // 구슬: 초록

        sealed class Particle
        {
            public SpriteRenderer R;
            public Vector3 Vel;
            public float Age, Life, Size, Gravity, Drag;
            public Color Col;
            public bool Active;
        }

        static HitFxRenderer s_instance;
        IWorld _world;
        int _layer;
        readonly List<Particle> _particles = new();
        readonly Dictionary<Entity, float> _kb = new();   // 엔티티별 지난 프레임 넉백 속도
        readonly List<Entity> _stale = new();
        readonly System.Random _rng = new(7);             // 연출 전용(판 난수 RunData.Rng와 무관)
        int _sparksThisFrame;

        public void Init(IWorld world)
        {
            _world = world;
            s_instance = this;
            int id = SortingLayer.NameToID("Projectiles");   // 캐릭터·체력바보다 위
            _layer = SortingLayer.IsValid(id) ? id : 0;
            _kb.Clear();
            ViewBridge.AnyDamaged -= OnDamaged;
            ViewBridge.AnyDamaged += OnDamaged;
        }

        void OnDestroy()
        {
            ViewBridge.AnyDamaged -= OnDamaged;
            if (s_instance == this) s_instance = null;
        }

        /// <summary>자리 pos에 색 c의 파편 n개를 speed로 터뜨린다(다른 연출도 쓸 수 있게 정적).</summary>
        public static void Burst(Vector3 pos, Color c, int n, float speed)
        {
            if (!s_instance || n <= 0) return;
            s_instance.Emit(pos, c, n, speed, 0.22f, 0.07f, 0.11f, 5f, FxSprites.DotSprite);
        }

        /// <summary>투사체가 사라진 자리(착탄·소멸) 파편. ProjectileRenderer가 부른다.</summary>
        public static void ProjectileGone(Vector3 pos, ProjectileVisual kind)
        {
            if (!s_instance) return;
            switch (kind)
            {
                case ProjectileVisual.SwordWave: s_instance.Emit(pos, WaveFrag, 3, 3f, 0.2f, 0.07f, 0.12f, 4f, FxSprites.DotSprite); break;
                case ProjectileVisual.Orb: s_instance.Emit(pos, OrbFrag, 4, 2.5f, 0.2f, 0.06f, 0.1f, 3f, FxSprites.DotSprite); break;
                default: s_instance.Emit(pos, ArrowFrag, 2, 2.5f, 0.2f, 0.05f, 0.08f, 6f, FxSprites.DotSprite); break;   // 화살·강화 화살
            }
        }

        void OnDamaged(DamagedEvent evt)
        {
            if (_world == null || evt.Amount <= 0) return;
            var em = _world.EntityManager;
            if (!em.Exists(evt.Victim) || !em.TryGetComponent<TransformComponent>(evt.Victim, out var t)) return;
            bool party = em.TryGetComponent<TeamComponent>(evt.Victim, out var team) && team.TeamFlag == Party.Team;
            bool kill = evt.RemainingHp <= 0;
            float w = HitShake.Weight(evt.Ratio, Sensitivity);

            // 스쿼시: 가로로 퍼지고 세로로 눌림. 막타는 크게, 파티원은 0.6배
            float sx = 1f + 0.12f * w, sy = 1f - 0.10f * w;
            if (kill && !party) { sx = 1.25f; sy = 0.75f; }
            if (party) { sx = 1f + (sx - 1f) * 0.6f; sy = 1f - (1f - sy) * 0.6f; }
            SpriteFx.Squash(evt.Victim, sx, sy, 0.08f, 0.16f);
            if (party) return;

            SpriteFx.Flash(evt.Victim, Color.white, kill ? 0.12f : evt.Ratio >= BigHitRatio ? 0.10f : 0.06f);

            // 스파크: 가슴 높이에서 노랗게, 피해 비율만큼 많이, 막타는 두 배
            float head = em.TryGetComponent<BodyComponent>(evt.Victim, out var body) ? body.HeadHeight : 1.2f;
            var chest = new Vector3(t.Position.X, t.Position.Y + t.Height + head * 0.55f, 0f);
            int n = 3 + Mathf.RoundToInt(6f * w);
            if (kill) n *= 2;
            if (Time.timeScale > 1.5f) n = Mathf.Max(1, n / 2);
            n = Mathf.Min(n, MaxSparksPerFrame - _sparksThisFrame);
            if (n <= 0) return;
            _sparksThisFrame += n;
            Emit(chest, SparkColor, n, kill ? 6f : 4.5f, 0.25f, 0.45f, 0.8f, 7f, FxSprites.StarSprite);
        }

        void LateUpdate()
        {
            _sparksThisFrame = 0;
            float dt = Time.unscaledDeltaTime;
            foreach (var p in _particles)
            {
                if (!p.Active) continue;
                p.Age += dt;
                if (p.Age >= p.Life) { p.Active = false; p.R.gameObject.SetActive(false); continue; }
                float k = p.Age / p.Life;
                p.Vel *= Mathf.Exp(-p.Drag * dt);
                p.Vel.y -= p.Gravity * dt;
                p.R.transform.position += p.Vel * dt;
                p.R.transform.localScale = Vector3.one * (p.Size * (1f - 0.5f * k));
                var c = p.Col; c.a *= 1f - k * k;
                p.R.color = c;
            }
            ScanKnockback();
        }

        // 넉백 속도가 거의 0에서 세게 뛰는 순간(= 밀리기 시작) 발밑 먼지. 읽기만 한다
        void ScanKnockback()
        {
            if (_world == null) return;
            var em = _world.EntityManager;
            foreach (var chunk in em.GetChunks(typeof(KnockbackComponent), typeof(TransformComponent)))
            {
                var kbs = chunk.GetComponentArray<KnockbackComponent>();
                var trs = chunk.GetComponentArray<TransformComponent>();
                var ents = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    var v = kbs[i].Velocity;
                    float sp = Mathf.Sqrt(v.X * v.X + v.Y * v.Y);
                    _kb.TryGetValue(ents[i], out float last);
                    if (last < KbDustFrom && sp >= KbDustTo) Dust(trs[i], v, sp);
                    _kb[ents[i]] = sp;
                }
            }
            if (_kb.Count == 0) return;
            foreach (var kv in _kb) if (!em.Exists(kv.Key)) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++) _kb.Remove(_stale[i]);
            _stale.Clear();
        }

        void Dust(in TransformComponent t, Vec2 v, float sp)
        {
            var feet = new Vector3(t.Position.X, t.Position.Y + t.Height, 0f);
            var away = new Vector2(-v.X / sp * 1.2f, 0.3f);   // 밀리는 반대쪽으로 낮게 흩어지며 살짝 떠오름
            Emit(feet, DustColor, 2, 0.6f, 0.3f, 0.12f, 0.18f, -0.6f, FxSprites.DotSprite, true, t.Position.Y, away);
        }

        // 파편 n개: 무작위 방향(위로 치우침) + bias. ground면 캐릭터 레이어(Default)에서 발 깊이 바로 앞, 아니면 Projectiles 위쪽
        void Emit(Vector3 pos, Color c, int n, float speed, float life, float sizeMin, float sizeMax, float gravity, Sprite sprite, bool ground = false, float depth = 0f, Vector2 bias = default)
        {
            for (int i = 0; i < n; i++)
            {
                var p = Get();
                if (p == null) return;
                float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float sp = speed * (0.55f + 0.7f * (float)_rng.NextDouble());
                p.Vel = new Vector3(Mathf.Cos(a) * sp + bias.x, Mathf.Sin(a) * sp * 0.8f + speed * 0.3f + bias.y, 0f);
                p.Age = 0f;
                p.Life = life * (0.8f + 0.4f * (float)_rng.NextDouble());
                p.Size = Mathf.Lerp(sizeMin, sizeMax, (float)_rng.NextDouble());
                p.Gravity = gravity; p.Drag = 4f; p.Col = c;
                p.R.sprite = sprite;
                p.R.transform.position = pos;
                p.R.transform.localScale = Vector3.one * p.Size;
                p.R.transform.localRotation = Quaternion.Euler(0f, 0f, (float)_rng.NextDouble() * 360f);
                p.R.color = c;
                p.R.sortingLayerID = ground ? 0 : _layer;
                p.R.sortingOrder = ground ? Mathf.RoundToInt(-depth * 100f) + 1 : SparkOrder;
                p.Active = true;
                if (!p.R.gameObject.activeSelf) p.R.gameObject.SetActive(true);
            }
        }

        Particle Get()
        {
            foreach (var p in _particles) if (!p.Active) return p;
            if (_particles.Count >= MaxParticles) return null;   // 상한: 더는 안 만든다
            var go = new GameObject("HitFx", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var np = new Particle { R = go.GetComponent<SpriteRenderer>() };
            _particles.Add(np);
            return np;
        }
    }
}
