using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.AI;
using IntoTheDungeon.Features.Stats;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Features.Attack
{
    public enum BasicSkillPreset : byte { None, Slash, QuickShot, ShieldBash }

    /// <summary>
    /// 기본 스킬: 캐릭터마다 하나. 필살기와 달리 버튼 없이, 재사용 대기가 돌고 마나가 있으면 AI가 공격 거리 안에서 알아서 쓴다.
    /// 정의·시전 상태는 필살기와 같은 모양(SpecialSkillComponent)을 그대로 쓴다.
    /// </summary>
    public struct BasicSkillComponent : IComponentData
    {
        public BasicSkillPreset Preset;
        public SpecialSkillComponent Skill;
    }

    /// <summary>기본 스킬 목록(임시값). 필살기보다 약하고 자주 나간다.</summary>
    public static class BasicSkills
    {
        public static string Name(BasicSkillPreset p) => p switch
        {
            BasicSkillPreset.Slash => "돌진 베기",
            BasicSkillPreset.QuickShot => "속사",
            BasicSkillPreset.ShieldBash => "방패 강타",
            _ => ""
        };

        public static string Description(BasicSkillPreset p) => p switch
        {
            BasicSkillPreset.Slash => "앞으로 뛰어들며 짧은 검기(공격력 ×2)",
            BasicSkillPreset.QuickShot => "빠른 관통 화살(공격력 ×1.8)",
            BasicSkillPreset.ShieldBash => "방패로 쳐서 밀치고 오래 굳힘(공격력 ×1.5)",
            _ => ""
        };

        public static BasicSkillComponent Create(BasicSkillPreset p)
        {
            var sk = p switch
            {
                // 케서린: 살짝 돌진하며 짧은 검기
                BasicSkillPreset.Slash => new SpecialSkillComponent
                {
                    Cooldown = 7f, CooldownRemaining = 3f, ManaCost = 12f,
                    CastTime = 0.2f, Duration = 0.5f, DashSpeed = 4f,
                    DamageMultiplier = 2f, WaveSpeed = 8f, WaveLifetime = 0.25f,   // 사거리 약 2
                    WaveHalfExtents = new Vec2(0.5f, 0.4f),
                    WaveKnockback = 3f, WaveLaunch = 0f, WaveHitStun = 0.3f,
                    Visual = ProjectileVisual.SwordWave
                },
                // 궁수: 제자리에서 빠르게 한 발 더(관통)
                BasicSkillPreset.QuickShot => new SpecialSkillComponent
                {
                    Cooldown = 6f, CooldownRemaining = 2f, ManaCost = 10f,
                    CastTime = 0.3f, Duration = 0.45f, DashSpeed = 0f,
                    DamageMultiplier = 1.8f, WaveSpeed = 16f, WaveLifetime = 0.55f,   // 사거리 약 9
                    WaveHalfExtents = new Vec2(0.4f, 0.25f),
                    WaveKnockback = 2f, WaveLaunch = 0f, WaveHitStun = 0.2f,
                    Visual = ProjectileVisual.PowerArrow
                },
                // 중갑 전사: 바로 앞을 방패로 쳐서 밀치고 길게 굳힌다
                BasicSkillPreset.ShieldBash => new SpecialSkillComponent
                {
                    Cooldown = 8f, CooldownRemaining = 3f, ManaCost = 12f,
                    CastTime = 0.25f, Duration = 0.6f, DashSpeed = 3f,
                    DamageMultiplier = 1.5f, WaveSpeed = 6f, WaveLifetime = 0.2f,   // 사거리 약 1.2
                    WaveHalfExtents = new Vec2(0.6f, 0.4f),
                    WaveKnockback = 5f, WaveLaunch = 0f, WaveHitStun = 0.8f,
                    Visual = ProjectileVisual.SwordWave
                },
                _ => default
            };
            return new BasicSkillComponent { Preset = p, Skill = sk };
        }
    }

    /// <summary>
    /// 기본 스킬 자동 발동: 전투 중, AI가 공격 거리 안(공격 상태)이고 평타를 휘두르는 중이 아닐 때,
    /// 재사용 대기가 끝났고 마나가 있으면 쓴다. 필살기 마나는 아껴 둔다(필살기가 곧 준비되면 남는 마나로만).
    /// 필살기를 쓰면 시전 중인 기본 스킬은 끊긴다(SpecialSkillSystem). SpecialSkillSystem 다음에 돈다.
    /// </summary>
    public sealed class BasicSkillSystem : GameSystem, ITick
    {
        const float SpecialSoon = 2f;   // 필살기 재사용 대기가 이만큼 안 남았으면 그 마나는 남겨 둔다

        IEventHub _hub;
        BattleProgress _progress;
        readonly List<(Entity owner, TeamFlag team, int damage, Vec2 pos, float height, float facing, SpecialSkillComponent skill)> _waves = new(4);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            Enabled = world.TryGet(out _hub) && world.TryGet(out _progress);
        }

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            _waves.Clear();
            bool fighting = _progress.Phase == BattlePhase.Fighting;

            foreach (var chunk in em.GetChunks(typeof(BasicSkillComponent), typeof(StateComponent), typeof(ActionPhaseComponent),
                                               typeof(StatusComponent), typeof(TransformComponent), typeof(TeamComponent)))
            {
                var basics = chunk.GetComponentArray<BasicSkillComponent>();
                var states = chunk.GetComponentArray<StateComponent>();
                var phases = chunk.GetComponentArray<ActionPhaseComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var sk = ref basics[i].Skill;
                    ref var state = ref states[i];
                    var e = entities[i];

                    if (sk.CooldownRemaining > 0f) sk.CooldownRemaining = System.MathF.Max(0f, sk.CooldownRemaining - dt);
                    if (state.Current.Control == ControlState.Dead) { sk.Casting = false; continue; }

                    if (sk.Casting)
                    {
                        sk.Timer += dt;
                        if (!sk.Fired && sk.Timer >= sk.CastTime)
                        {
                            sk.Fired = true;
                            _waves.Add((e, teams[i].TeamFlag, (int)System.MathF.Round(statuses[i].Damage * sk.DamageMultiplier),
                                        transforms[i].Position, transforms[i].Height, state.FacingToSign(), sk));
                        }
                        if (sk.Timer >= sk.Duration)
                        {
                            sk.Casting = false;
                            if (state.Current.Action == ActionState.Skill) SetAction(e, ref state, ActionState.Idle);
                        }
                        continue;
                    }

                    if (!fighting || sk.CooldownRemaining > 0f) continue;
                    if (state.Current.Action is ActionState.Skill or ActionState.Attack) continue;   // 필살기·평타 중엔 기다림
                    if (!state.CanAttack || transforms[i].Height > 0.01f) continue;
                    if (!em.TryGetComponent<CombatAIComponent>(e, out var ai) || ai.State != CombatAIState.Attack) continue;   // 공격 거리 안일 때만

                    bool hasMana = em.HasComponent<DerivedStatsComponent>(e);
                    if (hasMana)
                    {
                        float mana = em.GetComponent<DerivedStatsComponent>(e).Mana;
                        if (mana < sk.ManaCost) continue;
                        if (em.TryGetComponent<SpecialSkillComponent>(e, out var special) && special.CooldownRemaining <= SpecialSoon
                            && mana - sk.ManaCost < special.ManaCost) continue;   // 필살기 몫은 남긴다
                    }

                    ref var phase = ref phases[i];
                    phase.ActionPhase = ActionPhase.None;
                    phase.PhaseTimer = 0f;
                    phase.ReadyToAct = false;
                    phase.DirtyPhase = false;

                    sk.Casting = true;
                    sk.Timer = 0f;
                    sk.Fired = false;
                    sk.CooldownRemaining = sk.Cooldown;
                    if (hasMana) em.GetComponent<DerivedStatsComponent>(e).Mana -= sk.ManaCost;

                    state.MoveDir = Vec2.Zero;
                    SetAction(e, ref state, ActionState.Skill, stopMoving: true);
                    if (sk.DashSpeed > 0f && em.HasComponent<KnockbackComponent>(e))
                        em.GetComponent<KnockbackComponent>(e).Velocity = new Vec2(state.FacingToSign() * sk.DashSpeed, 0f);
                }
            }

            foreach (var w in _waves)
                SpecialSkillSystem.CreateWave(em, w.owner, w.team, w.damage, w.pos, w.height, w.facing, w.skill);
        }

        void SetAction(Entity e, ref StateComponent state, ActionState action, bool stopMoving = false)
        {
            var prev = state.Current;
            var next = prev;
            next.Action = action;
            if (stopMoving) next.Movement = MovementState.Idle;

            var mask = ChangeMask.None;
            if (prev.Action != next.Action) mask |= ChangeMask.Action;
            if (prev.Movement != next.Movement) mask |= ChangeMask.Movement;
            if (mask == ChangeMask.None) return;

            next.Version = prev.Version + 1;
            state.Current = next;
            _hub.Publish(StateChangedEvent.FromSnapshot(e, next, mask));
        }
    }
}
