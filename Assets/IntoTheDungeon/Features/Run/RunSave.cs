using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 이어하기용 판 저장 데이터(JsonUtility로 바꿀 수 있게 공개 필드만). 아이템은 ItemPool 번호, 카드는 Id로 적는다.
    /// 지도 방을 고르는 중이거나 정비 화면일 때만 저장하므로 전투 중에 끄면 그 방에 들어가기 전으로 돌아간다.
    /// </summary>
    [System.Serializable]
    public sealed class RunSave
    {
        public int Version = 1;
        public int Difficulty, Stage, Phase;
        public bool StageCleared, CanReturnToCamp, InShopRoom, RewardTaken;
        public int CurrentLayer = -1, CurrentIndex = -1;
        public int Gold, LastXp, LastGold, SealTier;
        public float LastHeal;
        public string LastMessage = "";
        public List<NodeSave> Nodes = new();
        public List<string> Members = new();
        public List<int> Formation = new();   // 슬롯별 배치 칸(없던 옛 저장은 비어 있음 → 기본 자리)
        public List<float> PartyHp = new();
        public List<int> Levels = new(), Xp = new(), LastLevelUps = new();
        public List<ItemSave> Items = new();
        public List<int> Loot = new();
        public List<string> Deck = new(), PendingRewards = new(), Relics = new();
        public List<OfferSave> Shop = new();
    }

    [System.Serializable]
    public sealed class NodeSave
    {
        public int Layer, Index, Type;
        public bool Visited;
        public List<int> Next = new();
    }

    [System.Serializable]
    public sealed class ItemSave
    {
        public int Owner;   // 파티 슬롯, 보관함은 -1
        public int Item, X, Y;
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
