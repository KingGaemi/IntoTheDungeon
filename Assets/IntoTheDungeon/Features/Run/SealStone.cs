namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 봉인석 단계(0~10, 임시값): 던전의 봉인을 풀수록 몹이 강해지고 그만큼 보상이 커진다.
    /// 지도·정비 화면에서 언제든 올리고 내릴 수 있고, 다음 전투부터 적용된다.
    /// 몹 체력이 오르면 처치 경험치·골드도 체력에 비례해 같이 오르고, 그 위에 보상 배율이 한 번 더 붙는다.
    /// </summary>
    public static class SealStone
    {
        public const int MaxTier = 10;

        public static float EnemyHpMul(int t) => 1f + 0.18f * t;        // 몹 체력
        public static float EnemyDamageMul(int t) => 1f + 0.12f * t;    // 몹 공격력
        public static float RewardMul(int t) => 1f + 0.12f * t;         // 처치 경험치·골드(체력 비례분 위에 더)
        public static float DropMul(int t) => 1f + 0.1f * t;            // 아이템 드랍 확률
        public static float RareBonus(int t) => 0.06f * t;              // 좋은 등급이 잘 나옴
        public static int EndGems(int t) => t / 2;                       // 판 끝 잼 보상 추가

        /// <summary>처치 경험치·골드가 실제로 몇 배가 되는지(체력 비례 × 보상 배율).</summary>
        public static float TotalRewardMul(int t) => EnemyHpMul(t) * RewardMul(t);
    }
}
