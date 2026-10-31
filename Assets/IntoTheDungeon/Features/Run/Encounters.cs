using System.Collections.Generic;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 방 종류·층별 몹 구성(임시값). 지금은 오크 1·2·3 스탯이 같아서 마릿수로만 난이도를 올린다.
    /// 종류별 스탯이 정해지면 여기 수치를 다시 맞춘다.
    /// </summary>
    public static class Encounters
    {
        public static List<WaveSpawn[]> For(RoomType type, int layer)
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
                    waves.Add(new[] { new WaveSpawn("Orc3", 3) });
                    waves.Add(new[] { new WaveSpawn("Orc2", 3), new WaveSpawn("Orc3", 2) });
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
