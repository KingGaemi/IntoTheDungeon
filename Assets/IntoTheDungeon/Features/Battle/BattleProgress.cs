using System.Collections.Generic;

namespace IntoTheDungeon.Features.Battle
{
    public enum BattlePhase { Intro, Fighting, Interlude, Victory, Defeat }

    /// <summary>
    /// 전투 한 판의 진행 상황. BattleFlowSystem이 쓰고 HUD가 읽는다(월드 서비스로 등록).
    /// </summary>
    public sealed class BattleProgress
    {
        public BattlePhase Phase = BattlePhase.Intro;
        public int Wave;          // 지금(또는 방금 끝난) 웨이브, 1부터. 시작 전 0
        public int TotalWaves;
        public int EnemiesAlive;
        public float PhaseTime;   // 지금 단계에 들어온 뒤 지난 시간(초)

        public bool IsOver => Phase is BattlePhase.Victory or BattlePhase.Defeat;
    }

    public readonly struct WaveSpawn
    {
        public readonly string Recipe;
        public readonly int Count;
        public WaveSpawn(string recipe, int count) { Recipe = recipe; Count = count; }
    }

    /// <summary>전투 한 판의 구성: 웨이브별 몹, 시작 동료 수, 연출 시간.</summary>
    public sealed class BattleConfig
    {
        public readonly List<WaveSpawn[]> Waves = new();
        public float IntroTime = 1.5f;       // "전투 시작" 뒤 첫 웨이브까지
        public float InterludeTime = 2f;     // 웨이브 사이 쉬는 시간
        public int StartingAllies = 2;       // 전투 시작 때 씬에 놓인 파티원 곁에 더 붙는 동료 수
        public string AllyRecipe = "Catherine";
        public float SpawnDistance = 6f;     // 파티 중심에서 이만큼 떨어진 곳(화면 밖 쪽)에 몹이 나온다
    }
}
