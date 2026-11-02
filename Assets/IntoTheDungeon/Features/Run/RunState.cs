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

        public DungeonMap Map { get; private set; }

        /// <summary>스테이지(1부터). 스테이지마다 새 지도, 마지막 스테이지 보스를 깨면 판 클리어.</summary>
        public int Stage { get; private set; } = 1;
        public const int MaxStage = 2;
        public bool IsLastStage => Stage >= MaxStage;
        /// <summary>이번 스테이지 보스를 깼다(정비를 마치고 지도로 가면 다음 스테이지).</summary>
        public bool StageCleared { get; private set; }

        /// <summary>던전 전체 깊이: 스테이지마다 지도 층 수만큼 더해진다(적 레벨·드랍 등급 계산용).</summary>
        public int Depth => (Stage - 1) * Map.LayerCount + System.Math.Max(0, CurrentLayer);
        public readonly Difficulty Difficulty;
        public MapNode Current { get; private set; }   // 마지막으로 들어간 방(시작 전 null)
        public RunPhase Phase { get; private set; } = RunPhase.ChoosingRoom;

        /// <summary>파티 슬롯별 체력 비율(0~1, 0 = 쓰러짐). 비어 있으면 전원 가득.</summary>
        public readonly List<float> PartyHp = new();
        public string LastMessage = "";
        public float LastHeal;   // 마지막 전투 후 기본 회복 비율(정비 화면 표시용, 장비 보너스 제외)

        /// <summary>파티원 레시피(슬롯 0번부터). 전투 때 이 순서로 스폰하고, 쓰러진 슬롯은 스폰하지 않는다. 영입하면 늘어난다.</summary>
        public readonly List<string> Members = new() { "Warrior", "Catherine", "Archer" };
        public int PartySize => Members.Count;

        /// <summary>파티원별 장비 칸(슬롯 순). 여기 놓인 아이템은 그 파티원에게만 효과가 있다.</summary>
        public readonly List<Inventory> Gear = new();

        /// <summary>파티원별 레벨·경험치(슬롯 순).</summary>
        public readonly List<int> Levels = new();
        public readonly List<int> Xp = new();
        public readonly List<int> LastLevelUps = new();   // 이번 방에서 레벨이 오른 슬롯(정비 화면 표시용)

        public int LevelOf(int slot) { Grow(slot); return Levels[slot]; }
        public int XpOf(int slot) { Grow(slot); return Xp[slot]; }
        public int LastXp, LastGold;   // 이번 방에서 처치로 얻은 경험치(살아 있는 파티원 기준)·골드(정비 화면 표시용)
        public int Gold;
        /// <summary>봉인석 단계(0 = 기본). SealStone 배율로 몹이 강해지고 보상이 커진다.</summary>
        public int SealTier;
        public void SetSealTier(int tier) => SealTier = System.Math.Clamp(tier, 0, SealStone.MaxTier);
        public static int XpToNext(int level) => 50 * level;   // 임시값: 50, 100, 150, ...

        void Grow(int slot)
        {
            while (Levels.Count <= slot) { Levels.Add(1); Xp.Add(0); }
        }

        /// <summary>경험치를 더하고 넘치면 레벨 업(여러 번도). 오른 레벨 수를 돌려준다.</summary>
        public int AddXp(int slot, int amount)
        {
            Grow(slot);
            Xp[slot] += amount;
            int ups = 0;
            while (Xp[slot] >= XpToNext(Levels[slot])) { Xp[slot] -= XpToNext(Levels[slot]); Levels[slot]++; ups++; }
            if (ups > 0 && !LastLevelUps.Contains(slot)) LastLevelUps.Add(slot);
            return ups;
        }

        /// <summary>
        /// 적 하나를 처치했을 때 경험치·골드(임시값). 최대 체력이 클수록(강한 몹·높은 레벨) 많이 준다.
        /// 경험치 = 최대 체력/25 × (1 + 0.2×(레벨-1)), 골드 = 경험치의 절반 정도에 ±30% 흔들림.
        /// </summary>
        public static int KillXp(int maxHp, int level) => System.Math.Max(1, (int)System.MathF.Round(maxHp / 25f * (1f + 0.2f * System.Math.Max(0, level - 1))));
        public int KillGold(int xp) => System.Math.Max(1, (int)System.MathF.Round(xp * 0.5f * (0.7f + 0.6f * (float)Rng.NextDouble())));

        /// <summary>파티 공용 보관함. 효과 없이 보관만 한다.</summary>
        public readonly Inventory Stash = new(Inventory.StashCols, Inventory.StashRows);

        /// <summary>그 슬롯의 장비 칸(파티원 수만큼 늘려 둔다).</summary>
        public Inventory GearOf(int slot)
        {
            while (Gear.Count <= slot) Gear.Add(new Inventory(Inventory.GearCols, Inventory.GearRows));
            return Gear[slot];
        }

        /// <summary>이번 전투에서 주운 아이템(정비 화면에서 가방에 넣거나 버림). 정비를 마치면 남은 건 사라진다.</summary>
        public readonly List<ItemDef> Loot = new();

        /// <summary>드랍 뽑기 등 판 안의 무작위.</summary>
        public System.Random Rng => _rng;

        /// <summary>얻은 카드들. 패시브는 전투 때 파티 전원에게 걸린다.</summary>
        public readonly List<CardDef> Deck = new();

        /// <summary>정비 화면에서 고를 보상(이번 전투 것). 하나 고르면 RewardTaken.</summary>
        public readonly List<CardDef> PendingRewards = new();
        public bool RewardTaken { get; private set; }

        /// <summary>지금 상점 품목: 전투 뒤 정비 화면엔 작은 상점, 상점 방에 들어가면 큰 상점.</summary>
        public readonly List<ShopOffer> Shop = new();
        /// <summary>상점 방에 들어와 있다(정비 화면을 상점이 열린 채로 쓰고, 고를 보상은 없다).</summary>
        public bool InShopRoom { get; private set; }
        /// <summary>정비 화면 밖에서 장비 칸·주운 아이템·파티 구성이 바뀌면 올린다(상점에서 사기 등). 화면이 보고 다시 그린다.</summary>
        public int InventoryVersion { get; private set; }

        readonly System.Random _rng;

        public RunData(int seed, Difficulty difficulty = Difficulty.Normal)
        {
            Difficulty = difficulty;
            Map = DungeonMap.Generate(seed);
            _rng = new System.Random(seed ^ 0x5f3759df);
        }

        RunData(int seed, Difficulty difficulty, DungeonMap map)
        {
            Difficulty = difficulty;
            Map = map;
            _rng = new System.Random(seed ^ 0x5f3759df);
        }

        /// <summary>그 파티원의 보정값 = 덱 패시브(파티 전원) + 그 파티원 장비 칸의 아이템.</summary>
        public PartyModifiers BuildModifiers(int slot)
        {
            var m = new PartyModifiers();
            foreach (var card in Deck)
                card.Modify?.Invoke(m);
            MetaProgress.Current.Apply(m);   // 잼 슬롯(파티 전원)
            if (slot >= 0)
                foreach (var item in GearOf(slot).Items)
                    item.Def.Modify?.Invoke(m);
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
            // 방을 고르면 정비는 끝: 돌아갈 수 없고, 가방에 못 넣은 아이템은 두고 간다
            CanReturnToCamp = false;
            Loot.Clear();
            LastXp = LastGold = 0;
            LastLevelUps.Clear();
            node.Visited = true;
            Current = node;
            if (node.Type == RoomType.Shop)
            {
                // 상점 방: 전투 없이 정비 화면을 상점이 열린 채로(보상 카드 없음)
                InShopRoom = true;
                Phase = RunPhase.Camp;
                PendingRewards.Clear();
                RewardTaken = true;
                Shop.Clear();
                Shop.AddRange(ShopPool.Generate(true, PartySize, Depth, _rng));
                LastMessage = "";
                return;
            }
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

        /// <summary>그 파티원의 전투 후 회복 비율 = 난이도 기본값 + 덱·장비 보정(0~1).</summary>
        public float PostBattleHealRatio(int slot)
            => System.Math.Clamp(DifficultyTable.PostBattleHeal(Difficulty) + BuildModifiers(slot).PostBattleHealAdd, 0f, 1f);

        /// <summary>전투에서 이겼다. 파티 체력을 기억하고(살아 있는 파티원은 전투 후 회복) 정비로 간다(보스였으면 판 클리어).</summary>
        public void WinRoom(IReadOnlyList<float> partyHp)
        {
            PartyHp.Clear();
            PartyHp.AddRange(partyHp);
            for (int i = 0; i < PartyHp.Count; i++)
                if (PartyHp[i] > 0f) PartyHp[i] = System.MathF.Min(1f, PartyHp[i] + PostBattleHealRatio(i));   // 쓰러진 파티원은 휴식 방에서만

            LastHeal = DifficultyTable.PostBattleHeal(Difficulty) + BuildModifiers(-1).PostBattleHealAdd;   // 표시용(덱까지, 장비는 파티원별)
            bool boss = Current != null && Current.Type == RoomType.Boss;
            Phase = boss && IsLastStage ? RunPhase.Cleared : RunPhase.Camp;
            if (boss && !IsLastStage) StageCleared = true;   // 정비 뒤 다음 스테이지
            if (boss) MetaProgress.Current.AddBossKill();      // 잼 슬롯 해금 기록
            if (Phase == RunPhase.Cleared) EndRun(true);

            // 전투 뒤 보상 카드는 없다(성장은 레벨 업, 카드·영입은 상점에서)
            PendingRewards.Clear();
            RewardTaken = true;
            InShopRoom = false;
            Shop.Clear();
            if (Phase == RunPhase.Camp && Current != null)
                Shop.AddRange(ShopPool.Generate(false, PartySize, Depth, _rng));
        }

        // ───────── 상점 ─────────

        bool AnyFallen() { for (int i = 0; i < PartySize; i++) if (i < PartyHp.Count && PartyHp[i] <= 0f) return true; return false; }
        bool AnyHurt() { if (PartyHp.Count == 0) return false; for (int i = 0; i < PartyHp.Count; i++) if (PartyHp[i] > 0f && PartyHp[i] < 1f) return true; return false; }

        /// <summary>지금 살 수 없는 이유(살 수 있으면 null).</summary>
        public string CannotBuy(ShopOffer o)
        {
            if (Phase != RunPhase.Camp || !Shop.Contains(o)) return "지금은 살 수 없습니다";
            if (o.Sold) return "품절";
            if (o.Kind == ShopOfferKind.Recruit && PartySize >= CardPool.MaxPartySize) return "파티가 가득 찼습니다";
            if (o.Kind == ShopOfferKind.Revive && !AnyFallen()) return "쓰러진 파티원이 없습니다";
            if (o.Kind == ShopOfferKind.Heal && !AnyHurt()) return "다친 파티원이 없습니다";
            if (Gold < o.Price) return "골드가 부족합니다";
            return null;
        }

        /// <summary>산다. 아이템은 주운 아이템 칸으로(정비 화면에서 끌어서 넣는다). 못 사면 이유를 돌려준다.</summary>
        public string Buy(ShopOffer o)
        {
            var why = CannotBuy(o);
            if (why != null) return why;
            Gold -= o.Price;
            switch (o.Kind)
            {
                case ShopOfferKind.Item:
                    Loot.Add(o.Item);
                    o.Sold = true;
                    break;
                case ShopOfferKind.Card:
                    Deck.Add(o.Card);
                    o.Sold = true;
                    break;
                case ShopOfferKind.Recruit:
                    Members.Add(o.Card.RecruitRecipe);
                    if (PartyHp.Count > 0) PartyHp.Add(1f);
                    Deck.Add(o.Card);
                    o.Sold = true;
                    break;
                case ShopOfferKind.Revive:   // 여러 번 살 수 있다
                    for (int i = 0; i < PartyHp.Count; i++)
                        if (PartyHp[i] <= 0f) { PartyHp[i] = ShopPool.ReviveRatio; break; }
                    break;
                case ShopOfferKind.Heal:
                    for (int i = 0; i < PartyHp.Count; i++)
                        if (PartyHp[i] > 0f) PartyHp[i] = System.MathF.Min(1f, PartyHp[i] + ShopPool.HealRatio);
                    o.Sold = true;
                    break;
            }
            InventoryVersion++;
            return null;
        }

        /// <summary>아이템을 판다(원래 값의 절반). 장비 칸·보관함·주운 아이템 칸에서 빼는 건 부르는 쪽이 한다.</summary>
        public int Sell(ItemDef def)
        {
            int price = ShopPool.SellPrice(def);
            Gold += price;
            return price;
        }

        // ───────── 저장 ─────────

        /// <summary>이어하기용으로 저장할 수 있는 때: 지도에서 방을 고르는 중이거나 정비 화면.</summary>
        public bool CanSave => Phase is RunPhase.ChoosingRoom or RunPhase.Camp;

        public RunSave ToSave()
        {
            var s = new RunSave
            {
                Difficulty = (int)Difficulty, Stage = Stage, Phase = (int)Phase,
                StageCleared = StageCleared, CanReturnToCamp = CanReturnToCamp, InShopRoom = InShopRoom, RewardTaken = RewardTaken,
                CurrentLayer = Current?.Layer ?? -1, CurrentIndex = Current?.Index ?? -1,
                Gold = Gold, SealTier = SealTier, LastXp = LastXp, LastGold = LastGold, LastHeal = LastHeal, LastMessage = LastMessage ?? ""
            };
            foreach (var row in Map.Layers)
                foreach (var n in row)
                    s.Nodes.Add(new NodeSave { Layer = n.Layer, Index = n.Index, Type = (int)n.Type, Visited = n.Visited, Next = new List<int>(n.Next) });
            s.Members.AddRange(Members);
            s.PartyHp.AddRange(PartyHp);
            s.Levels.AddRange(Levels);
            s.Xp.AddRange(Xp);
            s.LastLevelUps.AddRange(LastLevelUps);
            for (int slot = 0; slot < Gear.Count; slot++)
                foreach (var p in Gear[slot].Items) s.Items.Add(new ItemSave { Owner = slot, Item = p.Def.Index, X = p.X, Y = p.Y, Rotated = p.Rotated });
            foreach (var p in Stash.Items) s.Items.Add(new ItemSave { Owner = -1, Item = p.Def.Index, X = p.X, Y = p.Y, Rotated = p.Rotated });
            foreach (var d in Loot) s.Loot.Add(d.Index);
            foreach (var c in Deck) s.Deck.Add(c.Id);
            foreach (var c in PendingRewards) s.PendingRewards.Add(c.Id);
            foreach (var o in Shop)
                s.Shop.Add(new OfferSave { Kind = (int)o.Kind, Item = o.Item?.Index ?? -1, Card = o.Card?.Id ?? "", Price = o.Price, Sold = o.Sold });
            return s;
        }

        /// <summary>저장 데이터로 판을 되살린다. 알 수 없는 아이템·카드는 빼고, 지도가 깨졌으면 null.</summary>
        public static RunData FromSave(RunSave s)
        {
            if (s == null || s.Nodes == null || s.Nodes.Count == 0) return null;
            var map = new DungeonMap();
            foreach (var ns in s.Nodes)
            {
                while (map.Layers.Count <= ns.Layer) map.Layers.Add(new List<MapNode>());
                var node = new MapNode { Layer = ns.Layer, Index = ns.Index, Type = (RoomType)ns.Type, Visited = ns.Visited };
                if (ns.Next != null) node.Next.AddRange(ns.Next);
                map.Layers[ns.Layer].Add(node);
            }
            foreach (var row in map.Layers) row.Sort((a, b) => a.Index.CompareTo(b.Index));

            var run = new RunData(System.Environment.TickCount, (Difficulty)s.Difficulty, map)
            {
                Stage = System.Math.Max(1, s.Stage),
                StageCleared = s.StageCleared,
                Phase = (RunPhase)s.Phase,
                CanReturnToCamp = s.CanReturnToCamp,
                InShopRoom = s.InShopRoom,
                RewardTaken = s.RewardTaken,
                Gold = s.Gold, SealTier = System.Math.Clamp(s.SealTier, 0, SealStone.MaxTier), LastXp = s.LastXp, LastGold = s.LastGold, LastHeal = s.LastHeal,
                LastMessage = s.LastMessage ?? ""
            };
            if (!run.CanSave) run.Phase = RunPhase.ChoosingRoom;
            if (s.CurrentLayer >= 0 && s.CurrentLayer < map.LayerCount)
                run.Current = map.Layers[s.CurrentLayer].Find(n => n.Index == s.CurrentIndex);

            run.Members.Clear();
            run.Members.AddRange(s.Members);
            run.PartyHp.AddRange(s.PartyHp);
            run.Levels.AddRange(s.Levels);
            run.Xp.AddRange(s.Xp);
            run.LastLevelUps.AddRange(s.LastLevelUps);
            foreach (var it in s.Items)
            {
                var def = ItemPool.Get(it.Item);
                if (def == null) continue;
                var inv = it.Owner >= 0 ? run.GearOf(it.Owner) : run.Stash;
                if (inv.Place(def, it.X, it.Y, it.Rotated) == null) run.Loot.Add(def);   // 칸 규칙이 바뀌었으면 주운 아이템 칸으로
            }
            foreach (int i in s.Loot) { var def = ItemPool.Get(i); if (def != null) run.Loot.Add(def); }
            foreach (var id in s.Deck) { var c = CardPool.Find(id); if (c != null) run.Deck.Add(c); }
            foreach (var id in s.PendingRewards) { var c = CardPool.Find(id); if (c != null) run.PendingRewards.Add(c); }
            foreach (var os in s.Shop)
            {
                var o = new ShopOffer { Kind = (ShopOfferKind)os.Kind, Item = ItemPool.Get(os.Item), Card = CardPool.Find(os.Card), Price = os.Price, Sold = os.Sold };
                if (o.Kind == ShopOfferKind.Item && o.Item == null) continue;
                if (o.Kind is ShopOfferKind.Card or ShopOfferKind.Recruit && o.Card == null) continue;
                run.Shop.Add(o);
            }
            return run;
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

        /// <summary>정비 화면에서 지도로. 방을 고르기 전까진 ReturnToCamp로 다시 정비할 수 있다.</summary>
        public void LeaveCamp()
        {
            if (Phase != RunPhase.Camp) return;
            Phase = RunPhase.ChoosingRoom;
            CanReturnToCamp = !InShopRoom;   // 상점 방에서 나오면 상점으로 되돌아가지 않는다
            InShopRoom = false;
            if (StageCleared)
            {
                // 다음 스테이지: 새 지도 첫 층부터
                StageCleared = false;
                Stage++;
                Map = DungeonMap.Generate(_rng.Next());
                Current = null;
                LastMessage = $"스테이지 {Stage} 시작";
            }
        }

        /// <summary>지도에서 방을 고르기 전이면 정비 화면으로 돌아간다(보상·가방 다시 손보기).</summary>
        public void ReturnToCamp()
        {
            if (!CanReturnToCamp || Phase != RunPhase.ChoosingRoom) return;
            Phase = RunPhase.Camp;
        }

        public bool CanReturnToCamp { get; private set; }

        public void Lose()
        {
            Phase = RunPhase.Failed;
            EndRun(false);
        }

        /// <summary>이번 판에 얻은 잼 이름(처치 드랍 + 판 끝 보상, 결과 화면 표시용).</summary>
        public readonly List<string> GemsFound = new();
        bool _runEnded;

        /// <summary>잼 하나를 얻는다(판이 끝나도 남는다).</summary>
        public GemDef FindGem()
        {
            var gem = GemPool.Roll(_rng);
            MetaProgress.Current.AddGem(gem.Id);
            GemsFound.Add(gem.Name);
            return gem;
        }

        /// <summary>판 끝 보상(클리어든 전멸이든 한 번): 잼 1 + 지나온 깊이 3마다 1 + 클리어 3(임시값).</summary>
        void EndRun(bool cleared)
        {
            if (_runEnded) return;
            _runEnded = true;
            int gems = 1 + Depth / 3 + (cleared ? 3 : 0) + SealStone.EndGems(SealTier);
            for (int i = 0; i < gems; i++) FindGem();
            MetaProgress.Current.EndRun(cleared);
        }
    }

    public static class RunState
    {
        static RunData _current;

        public static RunData Current => _current ??= new RunData(System.Environment.TickCount);

        public static void StartNew(Difficulty difficulty = Difficulty.Normal) => _current = new RunData(System.Environment.TickCount, difficulty);

        /// <summary>이어하기: 저장에서 되살린 판으로 바꾼다.</summary>
        public static void Continue(RunData run) => _current = run;
    }
}
