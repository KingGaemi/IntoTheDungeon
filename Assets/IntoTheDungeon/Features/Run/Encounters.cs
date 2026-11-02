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
                    waves.Add(W(S("Orc3", 1), S("Plant1", 2), S("Bat1", 2)));
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
                    waves.Add(W(S("Orc2", 1), S("Plant2", 2), S("Bat1", 3)));
                    waves.Add(W(S("Orc2", 2), S("Plant3", 2)));
                    waves.Add(W(S("Orc3", 1), S("Plant3", 2), S("Plant2", 1)));
                    break;
                case RoomType.Boss:
                    waves.Add(W(S("Orc2", 2), S("Plant2", 2), S("Bat1", 3)));
                    waves.Add(W(S("Orc3", 1), S("Plant3", 2), S("Plant1", 2)));
                    waves.Add(W(S("Plant3", 1, boss: true), S("Orc2", 1), S("Plant2", 2), S("Bat1", 2)));
                    break;
            }
            return waves;
        }

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
