using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>
    /// 되살아나는 몹(해골 기사): 쓰러지면 뼈 무더기로 무너졌다가 DownTime 뒤 체력 HpRatio로 다시 일어난다(Charges번).
    /// 무너져 있는 동안 저지력 StopRequired 이상인 필살기(검기·강화 화살·도발)를 맞히면 완전히 부서져 다시 못 일어난다.
    /// 무너진 동안은 아직 살아 있는 적으로 센다(웨이브가 끝나지 않고, 보상·드랍·시체 정리도 기다린다).
    /// </summary>
    public struct ReviveComponent : IComponentData
    {
        public int Charges;          // 남은 부활 횟수
        public int StopRequired;     // 무너져 있을 때 부수는 데 필요한 저지력
        public float DownTime;       // 무너진 뒤 일어나기까지(초)
        public float RiseTime;       // 그중 마지막 이만큼은 일어나는 모션
        public float HpRatio;        // 일어날 때 체력 비율
        public bool Downed, Rising;
        public float Timer;

        public static ReviveComponent Create(int charges, int stopRequired) => new()
        {
            Charges = charges, StopRequired = stopRequired, DownTime = 4.5f, RiseTime = 0.9f, HpRatio = 0.6f
        };
    }

    public static class Revive
    {
        /// <summary>무너져 있는(곧 일어날) 몹인가. 보상·드랍·시체 정리·웨이브 끝 판정이 기다린다.</summary>
        public static bool IsDowned(IEntityManager em, Entity e) => em.TryGetComponent<ReviveComponent>(e, out var r) && r.Downed;

        /// <summary>히트박스가 없는 저지(도발 범위 등)로 무너진 몹을 쳤다. ReviveSystem이 처리한다.</summary>
        public static readonly List<(Entity e, int power)> ShatterRequests = new(8);
    }

    public sealed class ReviveSystem : GameSystem, ITick
    {
        static readonly Vec2 PileHalf = new(0.5f, 0.32f);   // 뼈 무더기 맞는 범위(바닥 평면 반폭)
        const float HeightReach = 0.9f;

        IEventHub _hub;
        IPhysicsOpQueue _physOps;
        readonly List<Entity> _rise = new(4), _up = new(4);
        readonly List<(Entity e, Vec2 pos, float height)> _piles = new(4);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!world.TryGet(out _hub)) Enabled = false;
            world.TryGet(out _physOps);
        }

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            _rise.Clear(); _up.Clear(); _piles.Clear();

            foreach (var chunk in em.GetChunks(typeof(ReviveComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var revives = chunk.GetComponentArray<ReviveComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var rv = ref revives[i];
                    ref var st = ref statuses[i];
                    if (!rv.Downed)
                    {
                        // 방금 쓰러졌고 부활이 남았으면 무너진다(보상·드랍은 아직)
                        if (st.IsAlive || rv.Charges <= 0 || em.HasComponent<KillRewardedTag>(entities[i])) continue;
                        rv.Downed = true;
                        rv.Rising = false;
                        rv.Timer = 0f;
                    }
                    rv.Timer += dt;
                    _piles.Add((entities[i], transforms[i].Position, transforms[i].Height));
                    if (!rv.Rising && rv.Timer >= rv.DownTime - rv.RiseTime) { rv.Rising = true; _rise.Add(entities[i]); }
                    if (rv.Timer >= rv.DownTime)
                    {
                        rv.Downed = rv.Rising = false;
                        rv.Charges--;
                        st.CurrentHp = System.Math.Max(1, (int)System.MathF.Round(st.MaxHp * rv.HpRatio));
                        st.IsAlive = true;
                        _up.Add(entities[i]);
                    }
                }
            }

            // 일어나는 모션(필살기 상태 = 거꾸로 돈 사망 모션)
            foreach (var e in _rise) SetState(em, e, ControlState.Dead, ActionState.Skill, ChangeMask.Action);
            // 다시 일어남: 조작 가능·몸 충돌 복구
            foreach (var e in _up)
            {
                SetState(em, e, ControlState.Normal, ActionState.Idle, ChangeMask.Control | ChangeMask.Action);
                if (_physOps != null && em.TryGetComponent<PhysicsBodyRef>(e, out var body) && body.Handle.IsValid)
                    _physOps.Enqueue(body.Handle, PhysicsOpKind.SetSolid, new Vec2(1f, 0f));
                if (em.HasComponent<HitStunComponent>(e)) { ref var hs = ref em.GetComponent<HitStunComponent>(e); hs.Remaining = 0f; hs.Pending = 0f; hs.GroggyTotal = 0f; }
                Feed(em, e, InterruptKind.Revived, 0, 0);
            }

            if (_piles.Count > 0)
            {
                // 저지력 있는 필살기 히트박스(검기·강화 화살)가 뼈 무더기에 닿았나
                foreach (var chunk in em.GetChunks(typeof(HitboxComponent), typeof(HitListComponent)))
                {
                    var hitboxes = chunk.GetComponentArray<HitboxComponent>();
                    var lists = chunk.GetComponentArray<HitListComponent>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var hb = hitboxes[i];
                        if (hb.StopPower <= 0) continue;
                        ref var hits = ref lists[i];
                        var min = hb.Center - hb.HalfExtents;
                        var max = hb.Center + hb.HalfExtents;
                        foreach (var (pe, pos, height) in _piles)
                        {
                            if (hits.AlreadyHit(pe.Index) || hits.IsFull) continue;
                            if (System.MathF.Abs(hb.Height - height) > HeightReach) continue;
                            var pmin = pos - PileHalf;
                            var pmax = pos + PileHalf;
                            if (min.X > pmax.X || max.X < pmin.X || min.Y > pmax.Y || max.Y < pmin.Y) continue;
                            hits.AddHit(pe.Index);
                            Shatter(em, pe, hb.StopPower);
                        }
                    }
                }
            }
            foreach (var (e, power) in Revive.ShatterRequests)
                if (em.Exists(e)) Shatter(em, e, power);
            Revive.ShatterRequests.Clear();
        }

        // 저지력이 요구치 이상이면 완전히 부서진다(더는 못 일어남 → 보상·드랍·시체 정리가 이어서 처리). 모자라면 「저지력 부족」
        void Shatter(IEntityManager em, Entity e, int power)
        {
            if (!em.HasComponent<ReviveComponent>(e)) return;
            ref var rv = ref em.GetComponent<ReviveComponent>(e);
            if (!rv.Downed) return;
            if (power < rv.StopRequired)
            {
                Feed(em, e, InterruptKind.StopFailed, power, rv.StopRequired);
                return;
            }
            bool wasRising = rv.Rising;
            rv.Downed = rv.Rising = false;
            rv.Charges = 0;
            if (wasRising) SetState(em, e, ControlState.Dead, ActionState.Idle, ChangeMask.Control | ChangeMask.Action);   // 일어나던 중이면 다시 무너지는 모션
            Feed(em, e, InterruptKind.Shattered, power, rv.StopRequired);
        }

        void SetState(IEntityManager em, Entity e, ControlState control, ActionState action, ChangeMask mask)
        {
            if (!em.HasComponent<StateComponent>(e)) return;
            ref var state = ref em.GetComponent<StateComponent>(e);
            var next = state.Current;
            next.Control = control;
            next.Action = action;
            next.Movement = MovementState.Idle;
            next.Version = state.Current.Version + 1;
            state.Current = next;
            _hub.Publish(StateChangedEvent.FromSnapshot(e, next, mask));
        }

        static void Feed(IEntityManager em, Entity e, InterruptKind kind, int power, int required)
        {
            var t = em.GetComponent<TransformComponent>(e);
            float head = em.TryGetComponent<BodyComponent>(e, out var body) ? body.HeadHeight : 1.2f;
            InterruptFeed.Pending.Add(new InterruptInfo { Kind = kind, Position = t.Position, Height = t.Height + head * 0.5f, Power = power, Required = required });
        }
    }
}
