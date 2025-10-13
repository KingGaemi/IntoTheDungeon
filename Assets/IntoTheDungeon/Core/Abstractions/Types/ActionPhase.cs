namespace IntoTheDungeon.Core.Abstractions.Types
{
    public enum ActionPhase
    {
        None,       // 아무 액션도 실행 중 아님
        Queued,     // 입력 큐에 들어와 실행 대기 중
        Windup,    // 공격 준비 동작 (선딜)
        Active,     // 실제 판정이 발생하는 구간
        Recovery,   // 후딜 (다음 입력 대기 불가)
        Cooldown    // 쿨타임 (액션은 끝났지만 재사용 불가)
    }
}
