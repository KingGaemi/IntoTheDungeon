using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Features.State;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 경직: 맞으면 잠깐 굳는다(ControlState.Stunned → 이동·공격 의도가 막히고, 휘두르던 평타도 끊긴다).
    /// HitDetectSystem이 Pending에 이번 프레임 받은 가장 긴 경직을 적어 두면 HitStunSystem이 적용한다.
    /// </summary>
    public struct HitStunComponent : IComponentData
    {
        public float Pending;      // 이번 프레임에 맞아서 들어온 경직(초). 여러 번 맞으면 가장 긴 것
        public float Remaining;    // 남은 경직(초)
        public float Resistance;   // 0 = 그대로, 0.5 = 절반, 1 = 경직 안 걸림(보스·버프용)

        public static readonly HitStunComponent Default = new() { Resistance = 0f };
    }

    /// <summary>
    /// 경직 적용·해제. 필살기 시전 중(ActionState.Skill)엔 걸리지 않는다(슈퍼아머).
    /// 경직 중에 또 맞으면 남은 시간과 새 경직 중 긴 쪽으로 갱신한다.
    /// DeathStateSystem 다음에 돌아 같은 프레임에 죽은 캐릭터를 경직으로 덮어쓰지 않는다.
    /// </summary>
    public sealed class HitStunSystem : GameSystem, ITick
    {
        IEventHub _hub;

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!world.TryGet(out _hub)) Enabled = false;
        }

        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(HitStunComponent), typeof(StateComponent), typeof(ActionPhaseComponent)))
            {
                var stuns = chunk.GetComponentArray<HitStunComponent>();
                var states = chunk.GetComponentArray<StateComponent>();
                var phases = chunk.GetComponentArray<ActionPhaseComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var stun = ref stuns[i];
                    ref var state = ref states[i];
                    var cur = state.Current;

                    float incoming = stun.Pending * (1f - System.Math.Clamp(stun.Resistance, 0f, 1f));
                    stun.Pending = 0f;

                    if (cur.Control == ControlState.Dead) { stun.Remaining = 0f; continue; }

                    if (incoming > 0f && cur.Action != ActionState.Skill)
                    {
                        if (incoming > stun.Remaining) stun.Remaining = incoming;
                        if (cur.Control != ControlState.Stunned)
                        {
                            // 휘두르던 평타는 끊는다(히트박스도 안 나가게)
                            ref var phase = ref phases[i];
                            phase.ActionPhase = ActionPhase.None;
                            phase.PhaseTimer = 0f;
                            phase.ReadyToAct = false;
                            phase.DirtyPhase = false;

                            state.MoveDir = default;
                            SetControl(entities[i], ref state, ControlState.Stunned);
                        }
                        continue;
                    }

                    if (cur.Control == ControlState.Stunned)
                    {
                        stun.Remaining -= dt;
                        if (stun.Remaining <= 0f)
                        {
                            stun.Remaining = 0f;
                            SetControl(entities[i], ref state, ControlState.Normal);
                        }
                    }
                }
            }
        }

        void SetControl(Entity e, ref StateComponent state, ControlState control)
        {
            var prev = state.Current;
            var next = prev;
            next.Control = control;
            if (control == ControlState.Stunned)
            {
                next.Action = ActionState.Idle;
                next.Movement = MovementState.Idle;
            }

            var mask = ChangeMask.Control;
            if (prev.Action != next.Action) mask |= ChangeMask.Action;
            if (prev.Movement != next.Movement) mask |= ChangeMask.Movement;

            next.Version = prev.Version + 1;
            state.Current = next;
            _hub.Publish(StateChangedEvent.FromSnapshot(e, next, mask));
        }
    }
}
