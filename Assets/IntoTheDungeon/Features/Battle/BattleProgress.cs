using System.Collections.Generic;

namespace IntoTheDungeon.Features.Battle
{
    // Map: 지도에서 방 고르는 중(전투 없음), Deploy: 방에 들어와 전투 전 배치(파티만 서 있음, 「전투 시작」을 누르면 Intro)
    public enum BattlePhase { Map, Intro, Fighting, Interlude, Victory, Defeat, Deploy }

    /// <summary>
    /// 전투 한 판의 진행 상황. BattleFlowSystem이 쓰고 HUD가 읽는다(월드 서비스로 등록).
    /// </summary>
    public sealed class BattleProgress
    {
        public BattlePhase Phase = BattlePhase.Map;
        public int Wave;          // 지금(또는 방금 끝난) 웨이브, 1부터. 시작 전 0
        public int TotalWaves;
        public int EnemiesAlive;
        public float PhaseTime;   // 지금 단계에 들어온 뒤 지난 시간(초)
        public float Fade;        // 화면 덮개(0 밝음 ~ 1 깜깜): 웨이브 사이 다음 구역으로 넘어갈 때

        // 배치 단계(DeployHud가 쓰고 BattleFlowSystem이 읽는다)
        public bool DeployConfirmed;   // 「전투 시작」
        public int DragSlot = -1;      // 끌고 있는 파티원 슬롯(-1 = 없음)
        public IntoTheDungeon.Core.Util.Vec2 DragPos;   // 끄는 중인 바닥 위치

        public bool IsOver => Phase is BattlePhase.Victory or BattlePhase.Defeat;
        public bool IsFighting => Phase is BattlePhase.Intro or BattlePhase.Fighting or BattlePhase.Interlude;
    }

    public readonly struct WaveSpawn
    {
        public readonly string Recipe;
        public readonly int Count;
        public readonly bool Boss;   // 보스로 나옴(크게·강하게·내려찍기 패턴)
        public WaveSpawn(string recipe, int count, bool boss = false) { Recipe = recipe; Count = count; Boss = boss; }
    }

    /// <summary>전투 한 판의 구성: 웨이브별 몹(방에 들어갈 때 Encounters로 채움), 연출 시간. 파티 구성은 RunState.</summary>
    public sealed class BattleConfig
    {
        public readonly List<WaveSpawn[]> Waves = new();
        public float IntroTime = 1.5f;       // "전투 시작" 뒤 첫 웨이브까지
        // 웨이브 사이 "안쪽으로 전진": 파티가 오른쪽으로 달려가고 → 어두워지고 → 왼쪽 시작 자리로 옮겨져 → 밝아진 뒤 다음 웨이브
        public float AdvanceRunTime = 1.3f;
        public float AdvanceFadeOut = 0.45f;
        public float AdvanceHold = 0.2f;
        public float AdvanceFadeIn = 0.45f;
        public float SpawnDistance = 6f;     // 파티 중심에서 이만큼 떨어진 곳(화면 밖 쪽)에 몹이 나온다
    }
}
