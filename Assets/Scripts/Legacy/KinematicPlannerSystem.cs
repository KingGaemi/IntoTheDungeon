#if false
using IntoTheDungeon.Core.ECS;
using System.Collections.Generic;

namespace IntoTheDungeon.Features.Gameplay.Systems.Physics
{
    public sealed class KinematicPlannerSystem : GameSystemBase
    {
       
        public static KinematicPlannerSystem Instance { get; private set; }

        private readonly List<CharacterCore> cc = new();


        bool Register(KinematicComponent kc)
        {

            return true;
        }
        bool Unregister(KinematicComponent kc)
        {

            return true;
        }
        bool Contains(KinematicComponent kc)
        {

            return true;
        }
        public void Tick(float dt)
        {
            if (!Enabled) return;

            var list = cc; // 가정: List<CharacterCore>
            for (int i = 0; i < list.Count; i++)
            {
                var core = list[i];
                if (!core) continue;

                var state = core.State;
                var status = core.Status;
                var kinematic = core.Kinematic;
                if (!state || !status || !kinematic) continue;

                switch (state.Movement) // 속성 축약: state.Current.Movement → Movement 프로퍼티 권장
                {
                    case MovementState.Move:
                        kinematic.direction = state.FacingDir.ToVector2();   // 단위벡터
                        kinematic.magnitude = status.movementSpeed;          // 속도 스칼라
                        break;

                    case MovementState.Idle:
                    default:
                        // 방향 유지, 속도만 0
                        kinematic.magnitude = 0f;
                        break;
                }
            }
        }
    }
    
}
#endif