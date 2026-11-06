namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 다음 던전 등급(임시값): 스테이지를 깨면 1·2·3번 중 하나를 고르고 그 스테이지 내내 적용된다(봉인석과 별개로 곱해짐).
    /// 난이도는 몬스터 레벨 하나로 표현한다: 몹 레벨이 LevelAdd만큼 오르고, 오른 레벨마다 체력·공격력이 붙는다.
    /// 경험치·금화는 레벨을 따라 오르고(KillXp), 드랍·잼 보너스는 여기서 더한다.
    /// </summary>
    public static class DungeonTier
    {
        public const int Count = 3;
        const float HpPerLevel = 0.2f, DamagePerLevel = 0.12f;   // (임시값) 오른 레벨 하나당 체력·공격력

        static readonly string[] Names = { "일반", "위험", "지옥" };
        static readonly int[] LevelAdds = { 0, 6, 40 };           // (임시값) 3번은 판 초반 전력으론 깰 수 없게
        static readonly float[] DropMuls = { 1f, 1.5f, 3f };
        static readonly float[] RareBonuses = { 0f, 0.4f, 1f };
        static readonly int[] EndGemAdds = { 0, 3, 12 };

        static int At(int tier) => System.Math.Clamp(tier, 0, Count - 1);

        public static string Name(int tier) => Names[At(tier)];
        public static int LevelAdd(int tier) => LevelAdds[At(tier)];
        public static float EnemyHpMul(int tier) => 1f + HpPerLevel * LevelAdd(tier);
        public static float EnemyDamageMul(int tier) => 1f + DamagePerLevel * LevelAdd(tier);
        public static float DropMul(int tier) => DropMuls[At(tier)];
        public static float RareBonus(int tier) => RareBonuses[At(tier)];
        public static int EndGems(int tier) => EndGemAdds[At(tier)];

        /// <summary>그 스테이지 몹 평균 레벨(층별 적 레벨 1 + 깊이/2의 가운데 값 + 등급 레벨).</summary>
        public static int AverageLevel(int stage, int layers, int tier)
        {
            float middleDepth = (stage - 1) * layers + (layers - 1) * 0.5f;
            return (int)System.MathF.Round(1f + middleDepth / 2f) + LevelAdd(tier);
        }

        /// <summary>등급 1번 대비 처치 경험치·금화 배율(KillXp의 레벨 항과 같은 식).</summary>
        public static float RewardMul(int stage, int layers, int tier)
        {
            int plain = AverageLevel(stage, layers, 0);
            return (1f + 0.2f * (plain + LevelAdd(tier) - 1)) / (1f + 0.2f * (plain - 1));
        }
    }

    public sealed partial class RunData
    {
        /// <summary>스테이지를 깬 정비에서 다음 던전 등급을 고르고 바로 들어간다.</summary>
        public void EnterNextStage(int tier)
        {
            if (!StageCleared) return;
            StageTier = System.Math.Clamp(tier, 0, DungeonTier.Count - 1);
            LeaveCamp();
        }

        /// <summary>파티 평균 레벨(던전 고르기 창 비교용).</summary>
        public int AveragePartyLevel()
        {
            if (PartySize <= 0) return 1;
            int sum = 0;
            for (int i = 0; i < PartySize; i++) sum += LevelOf(i);
            return (int)System.MathF.Round((float)sum / PartySize);
        }
    }
}
