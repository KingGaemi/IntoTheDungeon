using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.AI
{
    public enum EnemyAIState : byte { Idle, Chase, Attack }

    /// <summary>
    /// 가장 가까운 적을 쫓아가 근접 공격하는 단순 AI. 플레이어 입력처럼 CharacterIntent만 만든다.
    /// 2.5D 바닥 평면 기준: 가로 거리와 깊이 차이를 따로 본다(같은 줄에 서야 맞으므로).
    /// </summary>
    public struct EnemyAIComponent : IComponentData
    {
        public float AggroRange;      // 이 평면 거리 안의 적을 쫓는다
        public float AttackRange;     // 가로 거리가 이 안이면 공격 가능
        public float DepthTolerance;  // 깊이 차이가 이 안이면 같은 줄로 본다
        public float ChaseResume;     // 공격 범위에서 이만큼 더 벗어나야 다시 쫓는다(떨림 방지)
        public float AttackCooldown;  // 공격 한 번 끝나고 다음 공격까지 쉬는 시간(초)

        public EnemyAIState State;
        public bool WasAttacking;
        public float NextAttackAt;

        public static readonly EnemyAIComponent Default = new()
        {
            AggroRange = 6f,
            AttackRange = 1.0f,
            DepthTolerance = 0.3f,
            ChaseResume = 0.2f,
            AttackCooldown = 0.8f,
            State = EnemyAIState.Idle
        };
    }
}
