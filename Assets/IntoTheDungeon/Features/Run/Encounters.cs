using System.Collections.Generic;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 방 종류·층·스테이지별 몹 구성(임시값).
    /// </summary>
    public static class Encounters
    {
        /// <summary>스테이지 1은 오크, 스테이지 2는 식인 식물. 보스 방 마지막 웨이브엔 보스(크고 강하고 내려찍기 패턴)가 나온다.</summary>
        public static List<WaveSpawn[]> For(RoomType type, int layer, int stage = 1)
            => stage >= 2 ? Plants(type, layer) : Orcs(type, layer);

        static List<WaveSpawn[]> Orcs(RoomType type, int layer)
        {
            var waves = new List<WaveSpawn[]>();
            switch (type)
            {
                case RoomType.Battle:
                {
                    int count = 2 + layer / 2;          // 층이 오를수록 많아짐
                    int waveCount = layer < 2 ? 2 : 3;
                    for (int w = 0; w < waveCount; w++)
                    {
                        if (layer >= 2 && w == waveCount - 1)
                            waves.Add(new[] { new WaveSpawn("Orc1", count - 1), new WaveSpawn("Orc2", 1 + layer / 3) });
                        else
                            waves.Add(new[] { new WaveSpawn("Orc1", count) });
                    }
                    break;
                }
                case RoomType.Elite:
                    waves.Add(new[] { new WaveSpawn("Orc2", 2) });
                    waves.Add(new[] { new WaveSpawn("Orc2", 2), new WaveSpawn("Orc3", 1) });
                    waves.Add(new[] { new WaveSpawn("Orc3", 2) });
                    break;
                case RoomType.Boss:
                    waves.Add(new[] { new WaveSpawn("Orc2", 3) });
                    waves.Add(new[] { new WaveSpawn("Orc3", 2), new WaveSpawn("Orc1", 2) });
                    waves.Add(new[] { new WaveSpawn("Orc3", 1, boss: true), new WaveSpawn("Orc2", 2) });
                    break;
            }
            return waves;
        }

        // 스테이지 2(임시값): 마릿수가 더 많고 정예·보스 방 구성이 두껍다
        static List<WaveSpawn[]> Plants(RoomType type, int layer)
        {
            var waves = new List<WaveSpawn[]>();
            switch (type)
            {
                case RoomType.Battle:
                {
                    int count = 3 + layer / 2;
                    for (int w = 0; w < 3; w++)
                    {
                        if (w == 2)
                            waves.Add(new[] { new WaveSpawn("Plant1", count - 1), new WaveSpawn("Plant2", 1 + layer / 3) });
                        else
                            waves.Add(new[] { new WaveSpawn("Plant1", count) });
                    }
                    break;
                }
                case RoomType.Elite:
                    waves.Add(new[] { new WaveSpawn("Plant2", 2), new WaveSpawn("Plant1", 2) });
                    waves.Add(new[] { new WaveSpawn("Plant2", 2), new WaveSpawn("Plant3", 1) });
                    waves.Add(new[] { new WaveSpawn("Plant3", 2), new WaveSpawn("Plant2", 1) });
                    break;
                case RoomType.Boss:
                    waves.Add(new[] { new WaveSpawn("Plant2", 3), new WaveSpawn("Plant1", 2) });
                    waves.Add(new[] { new WaveSpawn("Plant3", 3) });
                    waves.Add(new[] { new WaveSpawn("Plant3", 1, boss: true), new WaveSpawn("Plant2", 2), new WaveSpawn("Plant1", 2) });
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
            _ => "?"
        };
    }
}
