using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.State
{
    /// <summary>
    /// DeathEvent를 받아 StateComponent를 Dead로 바꾸고 StateChangedEvent를 발행한다(View의 Death 애니메이션).
    /// </summary>
    public sealed class DeathStateSystem : GameSystem, ITick
    {
        IEventHub _hub;
        IPhysicsOpQueue _physOps;

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!_world.TryGet(out _hub)) Enabled = false;
            _world.TryGet(out _physOps);   // 없으면 시체 충돌 해제만 건너뛴다
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

                // 시체가 길을 막지 않도록 물리 바디를 끈다(그림은 그대로 남음)
                if (_physOps != null && em.TryGetComponent<PhysicsBodyRef>(evt.Victim, out var body) && body.Handle.IsValid)
                    _physOps.Enqueue(body.Handle, PhysicsOpKind.SetSimulated, new Vec2(0f, 0f));
            }

            // 이 두 이벤트는 다른 소비자가 없어 여기서 비운다.
            _hub.Clear<DeathEvent>();
            _hub.Clear<KillEvent>();
        }
    }
}
