using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 이어하기용 판 저장 데이터(JsonUtility로 바꿀 수 있게 공개 필드만). 아이템은 ItemPool 번호, 카드는 Id로 적는다.
    /// 지도 방을 고르는 중이거나 정비 화면일 때만 저장하므로 전투 중에 끄면 그 방에 들어가기 전으로 돌아간다.
    /// </summary>
    [System.Serializable]
    public sealed partial class RunSave   // partial: 트랙별 확장 필드(RunSave.Events.cs 등)
    {
        public int Version = 1;
        public int Difficulty, Stage, Phase;
        public bool StageCleared, CanReturnToCamp, InShopRoom, RewardTaken;
        public int CurrentLayer = -1, CurrentIndex = -1;
        public int Gold, LastXp, LastGold, SealTier;
        public int StageTier;   // 지금 스테이지 던전 등급(옛 저장엔 없음 = 0 일반)
        public float LastHeal;
        public string LastMessage = "";
        public List<NodeSave> Nodes = new();
        public List<string> Members = new();
        public List<int> Formation = new();   // 슬롯별 배치 칸(없던 옛 저장은 비어 있음 → 기본 자리)
        public List<float> PartyHp = new();
        public List<int> Levels = new(), Xp = new(), LastLevelUps = new();
        public List<ItemSave> Items = new();
        public List<int> Loot = new();
        public List<int> LootLevels = new();   // Loot와 같은 순서의 강화 단계(옛 저장엔 없음 = 0)
        public List<string> Deck = new(), PendingRewards = new(), Relics = new();
        public List<OfferSave> Shop = new();
        public List<string> Traits = new();          // 슬롯:특성Id
        public List<string> PendingTraits = new();   // 슬롯:레벨:Id,Id,Id
        // 확장(없던 옛 저장은 기본값으로 읽힌다)
        public int Seed;                             // 0이면 모름(옛 저장)
        public double PlaySeconds;
        public RunStatsSave Stats = new();
        public int ForcedEncounter = -1;             // RoomType 번호, -1 = 없음
        public int RewardTierBonus;
        public bool InRestRoom;
    }

    [System.Serializable]
    public sealed class NodeSave
    {
        public int Layer, Index, Type;
        public bool Visited;
        public string Ally;
        public List<int> Next = new();
    }

    [System.Serializable]
    public sealed class ItemSave
    {
        public int Owner;   // 파티 슬롯, 보관함은 -1
        public int Item, X, Y;
        public int Level;   // 강화 단계
        public bool Rotated;
    }

    [System.Serializable]
    public sealed class OfferSave
    {
        public int Kind, Item = -1, Price;
        public string Card = "";
        public bool Sold;
    }
}
