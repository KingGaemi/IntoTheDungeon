using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 판 통계(저장됨). 전투·보상 시스템이 올리고 결산·업적·정비 칩이 읽는다(순수 C#).
    /// 카운터는 더하기만 한다(어느 트랙이든 += 로 올린다).
    /// </summary>
    public sealed class RunStats
    {
        public int Kills, EliteKills, BossKills;
        public int Breaks, BreakFails;                 // 패턴 끊기 성공·저지력 부족
        public int Battles, FlawlessBattles;           // 전투 수·무피해(아무도 안 쓰러진) 전투 수
        public int Rooms, Rests, Shops, Events;
        public int GoldEarned, ItemsFound, PartyDowns;
        public int DeathStage, DeathLayer;             // 전멸한 자리(결산용)
        public long DamageTaken, DamageDealt;
        public string DeathCause = "";                 // 전멸시킨 몹 이름 등(결산용)
        public readonly int[] BreaksBySlot = new int[CardPool.MaxPartySize];
        public int LastBattleBreaks;                   // 이번 방 끊기 수(정비 칩용)
        public bool LastBattleFlawless;                // 이번 방 무피해(정비 칩용)

        public RunStatsSave ToSave()
        {
            var s = new RunStatsSave
            {
                Kills = Kills, EliteKills = EliteKills, BossKills = BossKills, Breaks = Breaks, BreakFails = BreakFails,
                Battles = Battles, FlawlessBattles = FlawlessBattles, Rooms = Rooms, Rests = Rests, Shops = Shops, Events = Events,
                GoldEarned = GoldEarned, ItemsFound = ItemsFound, PartyDowns = PartyDowns, DeathStage = DeathStage, DeathLayer = DeathLayer,
                DamageTaken = DamageTaken, DamageDealt = DamageDealt, DeathCause = DeathCause ?? "",
                LastBattleBreaks = LastBattleBreaks, LastBattleFlawless = LastBattleFlawless
            };
            s.BreaksBySlot.AddRange(BreaksBySlot);
            return s;
        }

        /// <summary>저장에서 복원. s == null(옛 저장)이면 아무것도 안 한다.</summary>
        public void CopyFrom(RunStatsSave s)
        {
            if (s == null) return;
            Kills = s.Kills; EliteKills = s.EliteKills; BossKills = s.BossKills; Breaks = s.Breaks; BreakFails = s.BreakFails;
            Battles = s.Battles; FlawlessBattles = s.FlawlessBattles; Rooms = s.Rooms; Rests = s.Rests; Shops = s.Shops; Events = s.Events;
            GoldEarned = s.GoldEarned; ItemsFound = s.ItemsFound; PartyDowns = s.PartyDowns; DeathStage = s.DeathStage; DeathLayer = s.DeathLayer;
            DamageTaken = s.DamageTaken; DamageDealt = s.DamageDealt; DeathCause = s.DeathCause ?? "";
            LastBattleBreaks = s.LastBattleBreaks; LastBattleFlawless = s.LastBattleFlawless;
            System.Array.Clear(BreaksBySlot, 0, BreaksBySlot.Length);
            if (s.BreaksBySlot != null)
                for (int i = 0; i < s.BreaksBySlot.Count && i < BreaksBySlot.Length; i++) BreaksBySlot[i] = s.BreaksBySlot[i];
        }
    }

    /// <summary>RunStats의 저장 모양(JsonUtility: 공개 필드만).</summary>
    [System.Serializable]
    public sealed class RunStatsSave
    {
        public int Kills, EliteKills, BossKills, Breaks, BreakFails, Battles, FlawlessBattles, Rooms, Rests, Shops, Events, GoldEarned, ItemsFound, PartyDowns, DeathStage, DeathLayer;
        public long DamageTaken, DamageDealt;
        public string DeathCause = "";
        public List<int> BreaksBySlot = new();
        public int LastBattleBreaks;
        public bool LastBattleFlawless;
    }
}
