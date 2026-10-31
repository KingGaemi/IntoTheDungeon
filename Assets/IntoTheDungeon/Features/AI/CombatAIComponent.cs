using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.AI
{
    public enum CombatAIState : byte { Idle, Chase, Attack, Follow }

    /// <summary>
    /// 팀 공용 전투 AI: 가장 가까운 다른 팀을 쫓아가 근접 공격한다. 플레이어 입력처럼 CharacterIntent만 만든다.
    /// 몹·동료·자동전투 중인 플레이어가 같은 컴포넌트를 쓴다(편은 TeamComponent로 갈린다).
    /// 2.5D 바닥 평면 기준: 가로 거리와 깊이 차이를 따로 본다(같은 줄에 서야 맞으므로).
    /// </summary>
    public struct CombatAIComponent : IComponentData
    {
        public bool Enabled;          // 꺼져 있으면 아무 의도도 만들지 않는다(플레이어는 자동전투 켤 때만 켜짐)
        public float AggroRange;      // 이 평면 거리 안의 적을 쫓는다
        public float AttackRange;     // 가로 거리가 이 안이면 공격 가능
        public float DepthTolerance;  // 깊이 차이가 이 안이면 같은 줄로 본다
        public float ChaseResume;     // 공격 범위에서 이만큼 더 벗어나야 다시 쫓는다(떨림 방지)
        public float AttackCooldown;  // 공격 한 번 끝나고 다음 공격까지 쉬는 시간(초)

        // 싸울 적이 없을 때 같은 팀 플레이어(리더) 곁으로 돌아간다. 플레이어 자신과 다른 팀은 해당 없음
        public bool FollowLeader;
        public float FollowDistance;  // 리더와 이보다 멀어지면 따라간다
        public float FollowStop;      // 이 안으로 들어오면 멈춘다(FollowDistance보다 작게)

        public float PauseTimer;      // 0보다 크면 그동안 AI를 쉰다(자동전투 중 직접 조작했을 때)

        public CombatAIState State;
        public bool WasAttacking;
        public float NextAttackAt;

        // 몹
        public static readonly CombatAIComponent Default = new()
        {
            Enabled = true,
            AggroRange = 6f,
            AttackRange = 1.0f,
            DepthTolerance = 0.3f,
            ChaseResume = 0.2f,
            AttackCooldown = 0.8f,
            FollowLeader = true,      // 몹은 플레이어와 팀이 달라 실제로는 따라가지 않는다. 동료로 스폰되면 따라간다
            FollowDistance = 2.0f,
            FollowStop = 1.2f,
            State = CombatAIState.Idle
        };

        // 플레이어 자동전투: 방 안의 적을 넓게 찾아간다. 처음엔 꺼져 있다
        public static readonly CombatAIComponent AutoBattle = new()
        {
            Enabled = false,
            AggroRange = 30f,
            AttackRange = 1.0f,
            DepthTolerance = 0.3f,
            ChaseResume = 0.2f,
            AttackCooldown = 0.2f,
            FollowLeader = false,
            State = CombatAIState.Idle
        };
    }
}
