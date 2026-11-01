using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    public enum RunPhase { ChoosingRoom, InRoom, Camp, Cleared, Failed }   // Camp: 전투 뒤 보상·덱 정비
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>난이도별 기본값(임시값). 난이도 선택 화면이 생기면 RunState.StartNew에 넘긴다.</summary>
    public static class DifficultyTable
    {
        /// <summary>파티가 받는 피해 배율(몹 공격력은 그대로 두고 난이도로 조절).</summary>
        public static float PartyDamageTaken(Difficulty d) => d switch
        {
            Difficulty.Easy => 0.6f,
            Difficulty.Hard => 1f,
            _ => 0.8f
        };

        /// <summary>전투에서 이긴 뒤 살아 있는 파티원이 회복하는 최대 체력 비율(덱 보정 전).</summary>
        public static float PostBattleHeal(Difficulty d) => d switch
        {
            Difficulty.Easy => 0.3f,
            Difficulty.Hard => 0.1f,
            _ => 0.2f
        };
    }

    /// <summary>
    /// 던전 한 판(런)의 진행: 지도, 지금 위치, 방과 방 사이에 이어지는 파티(구성·체력)와 덱.
    /// 방을 옮길 때마다 씬을 다시 불러오므로 씬 밖(정적)에 둔다. 플레이 모드를 새로 켜면 새 판.
    /// </summary>
    public sealed class RunData
    {
        public const float RestHealRatio = 0.3f;   // 휴식: 최대 체력의 30% 회복(쓰러진 파티원도 30%로 일으킴)

        public readonly DungeonMap Map;
        public readonly Difficulty Difficulty;
        public MapNode Current { get; private set; }   // 마지막으로 들어간 방(시작 전 null)
        public RunPhase Phase { get; private set; } = RunPhase.ChoosingRoom;

        /// <summary>파티 슬롯별 체력 비율(0~1, 0 = 쓰러짐). 비어 있으면 전원 가득.</summary>
        public readonly List<float> PartyHp = new();
        public string LastMessage = "";
        public float LastHeal;   // 마지막 전투 후 회복 비율(정비 화면 표시용)

        /// <summary>파티원 레시피(슬롯 0번부터). 전투 때 이 순서로 스폰하고, 쓰러진 슬롯은 스폰하지 않는다. 영입하면 늘어난다.</summary>
        public readonly List<string> Members = new() { "Catherine", "Catherine", "Archer" };
        public int PartySize => Members.Count;

        /// <summary>얻은 카드들. 패시브는 전투 때 파티 전원에게 걸린다.</summary>
        public readonly List<CardDef> Deck = new();

        /// <summary>정비 화면에서 고를 보상(이번 전투 것). 하나 고르면 RewardTaken.</summary>
        public readonly List<CardDef> PendingRewards = new();
        public bool RewardTaken { get; private set; }

        readonly System.Random _rng;

        public RunData(int seed, Difficulty difficulty = Difficulty.Normal)
        {
            Difficulty = difficulty;
            Map = DungeonMap.Generate(seed);
            _rng = new System.Random(seed ^ 0x5f3759df);
        }

        /// <summary>덱의 패시브를 모두 합친 보정값.</summary>
        public PartyModifiers BuildModifiers()
        {
            var m = new PartyModifiers();
            foreach (var card in Deck)
                card.Modify?.Invoke(m);
            return m;
        }

        public int CurrentLayer => Current?.Layer ?? -1;

        /// <summary>지금 고를 수 있는 방들.</summary>
        public IEnumerable<MapNode> Choices()
        {
            if (Phase != RunPhase.ChoosingRoom) yield break;
            if (Current == null) { foreach (var n in Map.Layers[0]) yield return n; yield break; }
            if (Current.Layer + 1 >= Map.LayerCount) yield break;
            var next = Map.Layers[Current.Layer + 1];
            foreach (int i in Current.Next) yield return next[i];
        }

        public bool CanEnter(MapNode node)
        {
            foreach (var n in Choices()) if (n == node) return true;
            return false;
        }

        /// <summary>방에 들어간다. 휴식 방은 바로 회복하고 다음 방을 고르는 상태로 남는다.</summary>
        public void Enter(MapNode node)
        {
            if (!CanEnter(node)) return;
            node.Visited = true;
            Current = node;
            if (node.Type == RoomType.Rest)
            {
                for (int i = 0; i < PartyHp.Count; i++)
                    PartyHp[i] = System.MathF.Min(1f, PartyHp[i] + RestHealRatio);
                LastMessage = PartyHp.Count > 0 ? "휴식: 파티 체력 30% 회복" : "휴식: 파티 체력이 이미 가득합니다";
                return;
            }
            LastMessage = "";
            Phase = RunPhase.InRoom;
        }

        /// <summary>전투 후 회복 비율 = 난이도 기본값 + 덱 보정(0~1).</summary>
        public float PostBattleHealRatio
            => System.Math.Clamp(DifficultyTable.PostBattleHeal(Difficulty) + BuildModifiers().PostBattleHealAdd, 0f, 1f);

        /// <summary>전투에서 이겼다. 파티 체력을 기억하고(살아 있는 파티원은 전투 후 회복) 정비로 간다(보스였으면 판 클리어).</summary>
        public void WinRoom(IReadOnlyList<float> partyHp)
        {
            PartyHp.Clear();
            PartyHp.AddRange(partyHp);
            float heal = PostBattleHealRatio;
            for (int i = 0; i < PartyHp.Count; i++)
                if (PartyHp[i] > 0f) PartyHp[i] = System.MathF.Min(1f, PartyHp[i] + heal);   // 쓰러진 파티원은 휴식 방에서만
            LastHeal = heal;
            Phase = Current != null && Current.Type == RoomType.Boss ? RunPhase.Cleared : RunPhase.Camp;

            PendingRewards.Clear();
            RewardTaken = false;
            if (Phase == RunPhase.Camp && Current != null)
                PendingRewards.AddRange(CardPool.Offer(Current.Type, PartySize, _rng));
        }

        /// <summary>보상 하나를 고른다. 동료 영입이면 파티에 합류(체력 가득), 패시브면 덱에 넣는다.</summary>
        public void TakeReward(CardDef card)
        {
            if (RewardTaken || Phase != RunPhase.Camp || !PendingRewards.Contains(card)) return;
            RewardTaken = true;
            if (card.Kind == CardKind.Recruit)
            {
                if (PartySize >= CardPool.MaxPartySize) return;
                Members.Add(card.RecruitRecipe);
                if (PartyHp.Count > 0) PartyHp.Add(1f);   // 비어 있으면 전원 가득으로 취급되므로 그대로
            }
            Deck.Add(card);
        }

        /// <summary>정비를 마치고 지도로.</summary>
        public void LeaveCamp()
        {
            if (Phase == RunPhase.Camp) Phase = RunPhase.ChoosingRoom;
        }

        public void Lose() => Phase = RunPhase.Failed;
    }

    public static class RunState
    {
        static RunData _current;

        public static RunData Current => _current ??= new RunData(System.Environment.TickCount);

        public static void StartNew(Difficulty difficulty = Difficulty.Normal) => _current = new RunData(System.Environment.TickCount, difficulty);
    }
}
