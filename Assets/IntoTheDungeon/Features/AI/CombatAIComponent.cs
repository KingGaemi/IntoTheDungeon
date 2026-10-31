using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.AI
{
    public enum CombatAIState : byte { Idle, Chase, Attack, Follow }

    /// <summary>
    /// 팀 공용 전투 AI: 가장 가까운 다른 팀을 쫓아가 근접 공격한다. 플레이어 입력처럼 CharacterIntent만 만든다.
    /// 메인 캐릭터 없이 파티원·몹 전원이 같은 컴포넌트를 쓴다(편은 TeamComponent로 갈린다).
    /// 2.5D 바닥 평면 기준: 가로 거리와 깊이 차이를 따로 본다(같은 줄에 서야 맞으므로).
    /// </summary>
    public struct CombatAIComponent : IComponentData
    {
        public bool Enabled;          // 꺼져 있으면 아무 의도도 만들지 않는다
        public float AggroRange;      // 이 평면 거리 안의 적을 쫓는다
        public float AttackRange;     // 가로 거리가 이 안이면 공격 가능
        public float MinRange;        // 원거리: 가로 거리가 이보다 가까우면 물러난다(0 = 근접, 물러나지 않음)
        public float DepthTolerance;  // 깊이 차이가 이 안이면 같은 줄로 본다
        public float ChaseResume;     // 공격 범위에서 이만큼 더 벗어나야 다시 쫓는다(떨림 방지)
        public float AttackCooldown;  // 공격 한 번 끝나고 다음 공격까지 쉬는 시간(초)

        // 싸울 적이 없을 때 파티 중심으로 모인다(파티원만 해당)
        public bool FollowLeader;
        public float FollowDistance;  // 파티 중심과 이보다 멀어지면 모이러 간다
        public float FollowStop;      // 이 안으로 들어오면 멈춘다(FollowDistance보다 작게)

        public float RetreatTime;     // 물러나기를 이어온 시간. 벽 등으로 못 물러나면 일정 시간 뒤 그 자리에서 쏜다
        public bool HoldGround;

        public CombatAIState State;
        public bool WasAttacking;
        public float NextAttackAt;

        public static readonly CombatAIComponent Default = new()
        {
            Enabled = true,
            AggroRange = 30f,         // 전투 방에 들어오면 방 안의 적과 모두 붙는다
            AttackRange = 1.0f,
            DepthTolerance = 0.3f,
            ChaseResume = 0.2f,
            AttackCooldown = 0.8f,
            FollowLeader = true,      // 파티원일 때만 효과(몹은 해당 없음)
            FollowDistance = 2.0f,
            FollowStop = 1.2f,
            State = CombatAIState.Idle
        };

    }
}
