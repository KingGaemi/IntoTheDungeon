using System.Collections.Generic;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 방 종류·층·스테이지별 몹 구성(임시값).
    /// </summary>
    public static class Encounters
    {
        /// <summary>
        /// 몹은 역할이 섞여 나온다: 오크(앞에서 버티고 밀어냄)·식물(약하지만 아픔)·박쥐(빠른 떼, 흡혈).
        /// 스테이지 1은 오크 중심에 식인초·박쥐가 섞이고, 스테이지 2는 식물 중심에 가시 덩굴·독꽃과 강한 오크가 섞인다.
        /// 보스 방 마지막 웨이브엔 보스(크고 강하고 내려찍기 패턴)가 나온다.
        /// 정예 방엔 해골 기사(쓰러지면 뼈 무더기가 됐다 다시 일어남 · 무더기일 때 저지력 15 필살기로 분쇄)가 나온다.
        /// 2스테이지는 해골 기사가 둘씩 나오고 보스 방에도 섞인다(템·레벨로 쉬워지는 걸 막는 벽).
        /// </summary>
        public static List<WaveSpawn[]> For(RoomType type, int layer, int stage = 1)
            => stage >= 2 ? Stage2(type, layer) : Stage1(type, layer);

        static WaveSpawn[] W(params WaveSpawn[] s) => s;
        static WaveSpawn S(string recipe, int count, bool boss = false) => new(recipe, count, boss);

        static List<WaveSpawn[]> Stage1(RoomType type, int layer)
        {
            var waves = new List<WaveSpawn[]>();
            int k = layer / 3;   // 층이 오를수록 조금씩 많아짐
            switch (type)
            {
                case RoomType.Battle:
                    if (layer == 0)
                    {
                        waves.Add(W(S("Orc1", 2)));
                        waves.Add(W(S("Orc1", 1), S("Bat1", 3)));
                    }
                    else if (layer == 1)
                    {
                        waves.Add(W(S("Orc1", 2), S("Bat1", 2)));
                        waves.Add(W(S("Orc1", 1), S("Plant1", 2)));
                    }
                    else
                    {
                        waves.Add(W(S("Orc1", 1 + k), S("Bat1", 2 + k)));
                        waves.Add(W(S("Orc1", 2), S("Plant1", 1 + k)));
                        waves.Add(W(S("Orc2", 1 + layer / 4), S("Plant1", 1), S("Bat1", 2)));
                    }
                    break;
                case RoomType.Elite:
                    waves.Add(W(S("Orc2", 2), S("Bat1", 3)));
                    waves.Add(W(S("Orc2", 1), S("Plant1", 2), S("Plant2", 1)));
                    waves.Add(W(S("Skeleton1", 1), S("Orc1", 1), S("Plant1", 1)));   // 해골 기사
                    break;
                case RoomType.Boss:
                    waves.Add(W(S("Orc2", 2), S("Plant1", 2)));
                    waves.Add(W(S("Orc2", 1), S("Plant2", 1), S("Bat1", 4)));
                    waves.Add(W(S("Orc3", 1, boss: true), S("Orc1", 2), S("Plant1", 1)));
                    break;
            }
            return waves;
        }

        // 스테이지 2(임시값): 식물 중심, 마릿수가 더 많고 정예·보스 방 구성이 두껍다
        static List<WaveSpawn[]> Stage2(RoomType type, int layer)
        {
            var waves = new List<WaveSpawn[]>();
            int k = layer / 3;
            switch (type)
            {
                case RoomType.Battle:
                    waves.Add(W(S("Plant1", 2 + layer / 2), S("Bat1", 2)));
                    waves.Add(W(S("Orc1", 2), S("Plant2", 1 + k), S("Plant1", 1)));
                    waves.Add(W(S("Orc2", 1), S("Plant1", 1 + layer / 2), S("Plant3", 1 + k)));
                    break;
                case RoomType.Elite:
                    waves.Add(W(S("Skeleton1", 1), S("Plant2", 2), S("Bat1", 3)));
                    waves.Add(W(S("Orc3", 1), S("Plant3", 2)));
                    waves.Add(W(S("Skeleton1", 2), S("Plant3", 1), S("Plant2", 1)));   // 해골 기사 둘
                    break;
                case RoomType.Boss:
                    waves.Add(W(S("Orc2", 2), S("Plant2", 2), S("Bat1", 3)));
                    waves.Add(W(S("Skeleton1", 1), S("Orc3", 1), S("Plant3", 2)));
                    waves.Add(W(S("Plant3", 1, boss: true), S("Orc2", 1), S("Plant2", 2), S("Bat1", 2)));
                    break;
            }
            return waves;
        }

        // ───────── 파티 인원 보정 ─────────
        // 몹 구성·수치는 3인 파티 기준. 케서린 혼자 시작하므로 싸울 수 있는 인원이 적으면 마릿수·체력·공격력을 줄인다(4명이면 조금 더 많이).

        /// <summary>이번 방에 싸우러 나가는 인원(쓰러진 파티원 제외, 최소 1).</summary>
        public static int Fighters(RunData run)
        {
            int n = 0;
            for (int s = 0; s < run.PartySize; s++)
                if (s >= run.PartyHp.Count || run.PartyHp[s] > 0f) n++;
            return System.Math.Max(1, n);
        }

        /// <summary>인원별 마릿수 배율(1명 0.5 · 2명 0.75 · 3명 1 · 4명 1.2). 보스는 그대로.</summary>
        public static float CountMul(int fighters) => fighters switch { <= 1 => 0.5f, 2 => 0.75f, 3 => 1f, _ => 1.2f };
        /// <summary>인원별 몹 체력 배율.</summary>
        public static float HpMul(int fighters) => fighters switch { <= 1 => 0.7f, 2 => 0.85f, _ => 1f };
        /// <summary>
        /// 스테이지·정예 보정(템·레벨로 갈수록 쉬워지는 걸 막는다): 2스테이지 체력 ×1.45·공격력 ×1.3,
        /// 정예 방은 거기에 체력 ×1.15·공격력 ×1.1(2스테이지 정예는 ×1.3·×1.2).
        /// </summary>
        public static float StageHpMul(int stage, bool elite) => (1f + 0.45f * System.Math.Max(0, stage - 1)) * (elite ? (stage >= 2 ? 1.3f : 1.15f) : 1f);
        public static float StageDamageMul(int stage, bool elite) => (1f + 0.3f * System.Math.Max(0, stage - 1)) * (elite ? (stage >= 2 ? 1.2f : 1.1f) : 1f);

        /// <summary>인원별 몹 공격력 배율.</summary>
        public static float DamageMul(int fighters) => fighters switch { <= 1 => 0.75f, 2 => 0.88f, _ => 1f };

        /// <summary>웨이브 마릿수를 인원에 맞춘다(각 몹 최소 1마리, 보스는 그대로).</summary>
        public static List<WaveSpawn[]> ForParty(List<WaveSpawn[]> waves, int fighters)
        {
            float mul = CountMul(fighters);
            if (mul == 1f) return waves;
            for (int w = 0; w < waves.Count; w++)
            {
                var src = waves[w];
                var dst = new WaveSpawn[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var s = src[i];
                    int n = s.Boss ? s.Count : System.Math.Max(1, (int)System.MathF.Floor(s.Count * mul + 0.5f));
                    dst[i] = new WaveSpawn(s.Recipe, n, s.Boss);
                }
                waves[w] = dst;
            }
            return waves;
        }

        /// <summary>그 스테이지 보스가 확정으로 주는 보스 잼(지도 보스 칸에 미리 보여 준다). UnityCoreInstaller의 bossGem과 맞춘다.</summary>
        public static string BossGemOf(int stage) => stage >= 2 ? "bloom" : "warlord";
        /// <summary>정예 방 해골 기사가 확률로 주는 보스 잼.</summary>
        public const string EliteGem = "bonecrown";

        public static string Label(RoomType type) => type switch
        {
            RoomType.Battle => "전투",
            RoomType.Elite => "정예",
            RoomType.Rest => "휴식",
            RoomType.Boss => "보스",
            RoomType.Shop => "상점",
            _ => "?"
        };
    }
}
