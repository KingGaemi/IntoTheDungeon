using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Features.State
{
    /// <summary>
    /// DeathEvent를 받아 StateComponent를 Dead로 바꾸고 StateChangedEvent를 발행한다(View의 Death 애니메이션).
    /// </summary>
    public sealed class DeathStateSystem : GameSystem, ITick
    {
        IEventHub _hub;

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!_world.TryGet(out _hub)) Enabled = false;
        }

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            foreach (ref readonly var evt in _hub.Consume<DeathEvent>())
            {
                if (!em.Exists(evt.Victim) || !em.HasComponent<StateComponent>(evt.Victim)) continue;

                ref var state = ref em.GetComponent<StateComponent>(evt.Victim);
                var next = state.Current;
                next.Control = ControlState.Dead;
                next.Action = ActionState.Idle;
                next.Movement = MovementState.Idle;
                next.Version = state.Current.Version + 1;
                state.Current = next;

                _hub.Publish(StateChangedEvent.FromSnapshot(evt.Victim, next,
                    ChangeMask.Control | ChangeMask.Action | ChangeMask.Movement));
            }

            // 이 두 이벤트는 다른 소비자가 없어 여기서 비운다.
            _hub.Clear<DeathEvent>();
            _hub.Clear<KillEvent>();
        }
    }
}
