using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    public enum RunPhase { ChoosingRoom, InRoom, Camp, Cleared, Failed }   // Camp: 전투 뒤 보상·장비 정비
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

        /// <summary>전투에서 이긴 뒤 살아 있는 파티원이 회복하는 최대 체력 비율(유물·장비 보정 전).</summary>
        public static float PostBattleHeal(Difficulty d) => d switch
        {
            Difficulty.Easy => 0.3f,
            Difficulty.Hard => 0.1f,
            _ => 0.2f
        };
    }

    /// <summary>
    /// 던전 한 판(런)의 진행: 지도, 지금 위치, 방과 방 사이에 이어지는 파티(구성·체력)와 유물.
    /// 방을 옮길 때마다 씬을 다시 불러오므로 씬 밖(정적)에 둔다. 플레이 모드를 새로 켜면 새 판.
    /// </summary>
    public sealed partial class RunData
    {
        // 부분 메서드 훅(구현은 각 트랙: SaveEvents/LoadEvents = events, OnRunEnded = result-records. 없으면 호출이 사라진다)
        partial void SaveEvents(RunSave s);      // ToSave 끝
        partial void LoadEvents(RunSave s);      // FromSave 끝(return 직전)
        partial void OnRunEnded(bool cleared);   // EndRun에서 MetaProgress.Current.EndRun(cleared) 다음

        /// <summary>판 시드(지도 생성·난수의 뿌리). 결산 표시·재도전(StartNew(seed:))용.</summary>
        public int Seed { get; private set; }
        /// <summary>판 통계(저장됨). 결산·업적용 카운터.</summary>
        public readonly RunStats Stats = new();
        /// <summary>실시간 플레이 초(RunAutoSave가 더한다 — result-records 트랙).</summary>
        public double PlaySeconds;
        /// <summary>다음 전투 구성 강제(매복: Elite). BeginRoom이 읽고 WinRoom이 지운다.</summary>
        public RoomType? ForcedEncounter;
        /// <summary>다음 승리 보상 유물 티어 +(매복). WinRoom에서 쓰고 0으로.</summary>
        public int RewardTierBonus;
        /// <summary>휴식 방 정비 중(상점·보상 없음). events 트랙이 EnterRest에서 세운다.</summary>
        public bool InRestRoom { get; private set; }

        public const float ClearReviveRatio = 0.1f;   // 방을 깨면 쓰러진 파티원이 최대 체력의 10%로 일어난다
        public const float RestHealRatio = 0.3f;   // 휴식: 최대 체력의 30% 회복(쓰러진 파티원도 30%로 일으킴)
        // 물음표 방(임시값): 이 확률로 동료를 만난다(처음 하는 판은 확정). 아니면 금화 또는 샘물
        public const double EventAllyChance = 0.5;
        public const float SpringHealRatio = 0.25f;

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
        public readonly List<string> Members = new() { CardPool.DefaultStarter };   // 고른 시작 캐릭터 혼자 시작, 영입으로 최대 4명
        public int PartySize => Members.Count;

        /// <summary>파티원별 배치 칸(슬롯 순, Battle.Formation 칸 번호). -1이면 아직 없음 → 전투 전에 기본 자리로 채운다.</summary>
        public readonly List<int> Formation = new();

        public int CellOf(int slot) => slot >= 0 && slot < Formation.Count ? Formation[slot] : -1;

        /// <summary>빈 슬롯(새로 영입 등)과 겹친 칸을 기본 자리로 채운다. 근접은 전열부터, 원거리는 후열부터.</summary>
        public void EnsureFormation(System.Func<string, bool> isRanged)
        {
            while (Formation.Count < Members.Count) Formation.Add(-1);
            var used = new HashSet<int>();
            for (int s = 0; s < Members.Count; s++)
            {
                int c = Formation[s];
                if (c < 0 || c >= Battle.Formation.Cells || used.Contains(c)) Formation[s] = -1;
                else used.Add(c);
            }
            for (int s = 0; s < Members.Count; s++)
            {
                if (Formation[s] >= 0) continue;
                foreach (int c in Battle.Formation.Preference(isRanged(Members[s])))
                    if (used.Add(c)) { Formation[s] = c; break; }
            }
        }

        /// <summary>그 칸에 놓는다. 다른 파티원이 있던 칸이면 서로 자리를 바꾼다.</summary>
        public void PlaceInCell(int slot, int cell)
        {
            if (slot < 0 || slot >= Formation.Count || cell < 0 || cell >= Battle.Formation.Cells) return;
            int other = Formation.IndexOf(cell);
            if (other >= 0 && other != slot) Formation[other] = Formation[slot];
            Formation[slot] = cell;
        }

        /// <summary>파티원별 장비 칸(슬롯 순). 여기 놓인 아이템은 그 파티원에게만 효과가 있다.</summary>
        public readonly List<Inventory> Gear = new();

        /// <summary>파티원별 레벨·경험치(슬롯 순).</summary>
        public readonly List<int> Levels = new();
        public readonly List<int> Xp = new();
        public readonly List<int> LastLevelUps = new();   // 이번 방에서 레벨이 오른 슬롯(정비 화면 표시용)

        /// <summary>파티원별 레벨업 특성(슬롯 순, 특성 Id). 그 파티원에게만 걸린다.</summary>
        public readonly List<List<string>> Traits = new();
        /// <summary>아직 안 고른 레벨업 특성(오른 레벨마다 하나, 정비 화면에서 앞에서부터 고른다).</summary>
        public readonly List<TraitPick> PendingTraits = new();

        public List<string> TraitsOf(int slot)
        {
            while (Traits.Count <= slot) Traits.Add(new List<string>());
            return Traits[slot];
        }

        /// <summary>특성 하나를 고른다(그 고르기에 있던 것만).</summary>
        public void TakeTrait(TraitPick pick, string id)
        {
            if (!PendingTraits.Contains(pick) || !pick.Options.Contains(id)) return;
            PendingTraits.Remove(pick);
            if (pick.Slot < Members.Count && !TraitsOf(pick.Slot).Contains(id)) TraitsOf(pick.Slot).Add(id);
            InventoryVersion++;
        }

        public int LevelOf(int slot) { Grow(slot); return Levels[slot]; }
        public int XpOf(int slot) { Grow(slot); return Xp[slot]; }
        public int LastXp, LastGold;   // 이번 방에서 처치로 얻은 경험치(살아 있는 파티원 기준)·골드(정비 화면 표시용)
        public int Gold;
        /// <summary>봉인석 단계(0 = 기본). SealStone 배율로 몹이 강해지고 보상이 커진다.</summary>
        public int SealTier;
        public int StageTier;   // 지금 스테이지 던전 등급(DungeonTier, 스테이지를 깨면 다시 고른다)
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
            while (Xp[slot] >= XpToNext(Levels[slot]))
            {
                Xp[slot] -= XpToNext(Levels[slot]);
                Levels[slot]++;
                ups++;
                if (slot < Members.Count)   // 오른 레벨마다 특성 고르기 하나
                {
                    var owned = new List<string>(TraitsOf(slot));
                    foreach (var p in PendingTraits) if (p.Slot == slot) owned.AddRange(p.Options.FindAll(o => TraitPool.Find(o)?.SkillSwap == true));   // 교체는 한 번만 나오게
                    var pick = TraitPool.Roll(Members[slot], owned, slot, Levels[slot], _rng);
                    if (pick.Options.Count > 0) PendingTraits.Add(pick);
                }
            }
            if (ups > 0 && !LastLevelUps.Contains(slot)) LastLevelUps.Add(slot);
            return ups;
        }

        /// <summary>
        /// 적 하나를 처치했을 때 경험치·골드(임시값). 최대 체력이 클수록(강한 몹·높은 레벨) 많이 준다.
        /// 경험치 = 최대 체력/33 × (1 + 0.2×(레벨-1)), 골드 = 경험치의 0.9배 정도에 ±30% 흔들림(0.5 → 0.9: 골드가 너무 적음).
        /// </summary>
        public static int KillXp(int maxHp, int level) => System.Math.Max(1, (int)System.MathF.Round(maxHp / 33f * (1f + 0.2f * System.Math.Max(0, level - 1))));
        public int KillGold(int xp) => System.Math.Max(1, (int)System.MathF.Round(xp * 0.9f * (0.7f + 0.6f * (float)Rng.NextDouble())));

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

        /// <summary>얻은 카드들(동료 영입 기록). 파티 강화는 유물로 옮겨 갔다.</summary>
        public readonly List<CardDef> Deck = new();

        /// <summary>파티 공용 유물(개수 제한 없음). 파티 전원에게 걸린다.</summary>
        public readonly List<CardDef> Relics = new();

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

        public RunData(int seed, Difficulty difficulty = Difficulty.Normal, string starter = null)
        {
            Seed = seed;
            Difficulty = difficulty;
            if (CardPool.RecruitOf(starter) != null && MetaProgress.Current.AllyUnlocked(starter)) Members[0] = starter;
            bool tutorial = MetaProgress.Current.FirstRun;   // 처음 하는 판: 외길, 2층 물음표 방에서 중갑 전사를 확정으로 만난다
            Map = DungeonMap.Generate(seed, linear: tutorial);
            DungeonMap.PlaceAllies(Map, Stage, tutorial);
            _rng = new System.Random(seed ^ 0x5f3759df);
            foreach (var m in Members) MetaProgress.Current.MeetAlly(m);
        }

        RunData(int seed, Difficulty difficulty, DungeonMap map)
        {
            Seed = seed;
            Difficulty = difficulty;
            Map = map;
            _rng = new System.Random(seed ^ 0x5f3759df);
        }

        /// <summary>그 파티원의 보정값 = 파티 유물(파티 전원) + 잼 + 그 파티원 장비 칸의 아이템.</summary>
        public PartyModifiers BuildModifiers(int slot)
        {
            var m = new PartyModifiers();
            foreach (var relic in Relics)
                relic.Modify?.Invoke(m);
            MetaProgress.Current.Apply(m);   // 잼 슬롯(파티 전원)
            if (slot >= 0)
            {
                foreach (var item in GearOf(slot).Items)
                    item.Def.Modify?.Invoke(m);
                foreach (var id in TraitsOf(slot))
                    TraitPool.Find(id)?.Modify?.Invoke(m);
            }
            return m;
        }

        public int CurrentLayer => Current?.Layer ?? -1;

        /// <summary>판의 첫 전투(시작 캐릭터 혼자 몸풀기): 박쥐만 나오다 마지막에 오크 전사 하나, 오크 강타는 아프지만 죽지는 않는다.</summary>
        public bool TutorialFirstBattle => Stage == 1 && CurrentLayer == 0;

        /// <summary>이 방에서 함께 싸우다 합류할 동료(아직 파티에 없고 자리가 있을 때만). 없으면 null.</summary>
        public string GuestAlly => Current?.Ally != null && !Members.Contains(Current.Ally) && PartySize < CardPool.MaxPartySize ? Current.Ally : null;

        /// <summary>그 방의 동료가 아직 합류 전인가(지도 표시용).</summary>
        public bool AllyWaiting(MapNode node) => node.Ally != null && !Members.Contains(node.Ally) && PartySize < CardPool.MaxPartySize;

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
            PendingEvent = null;   // 남아 있던 이벤트 창은 방을 고르면 버린다(방어)
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
                Stats.Shops++;
                Phase = RunPhase.Camp;
                PendingRewards.Clear();
                RewardTaken = true;
                Shop.Clear();
                Shop.AddRange(ShopPool.Generate(true, this, _rng));
                LastMessage = "";
                return;
            }
            if (node.Type == RoomType.Event)
            {
                Stats.Events++;
                EnterEvent();
                return;
            }
            if (node.Type == RoomType.Rest)
            {
                EnterRest();
                return;
            }
            LastMessage = "";
            Phase = RunPhase.InRoom;
            if (GuestAlly != null) MetaProgress.Current.MeetAlly(GuestAlly);   // 동료 방: 만나기만 해도 해금
        }

        /// <summary>휴식 방: 상점·보상 없는 정비 화면을 열고 그 위에 3택(회복·단련·잼 연마) 모달을 건다(EventChoiceHud). 나가면 「정비로 돌아가기」는 없다.</summary>
        void EnterRest()
        {
            Stats.Rests++;
            Phase = RunPhase.Camp;
            InRestRoom = true;
            InShopRoom = false;
            PendingRewards.Clear();
            RewardTaken = true;
            Shop.Clear();
            PendingEvent = EventPool.Find(EventPool.RestId);
            if (PendingEvent == null)   // 정의가 없을 때만 옛 방식(바로 30% 회복)
            {
                EnsurePartyHp();
                HealLiving(RestHealRatio, fallenTo: RestHealRatio);
            }
            LastMessage = "";
            InventoryVersion++;
        }

        /// <summary>
        /// 물음표 방: 확률로 동료를 만난다 → 몹에 둘러싸여 싸우는 동료와 함께 전투, 이기면 합류(동료 방과 같음). 만나기만 해도 처음 본 유형은 해금.
        /// 1스테이지 2층 물음표 방은 동료 확정(판마다 무작위, 방에 정해져 있으면 그 동료 = 처음 하는 판의 중갑 전사).
        /// 못 만나면 선택 이벤트(EventPool.Roll, 지도 위 모달에서 고른다). 남은 이벤트가 없으면 금화를 바로 받고 지도에 남는다.
        /// </summary>
        void EnterEvent()
        {
            PendingRewards.Clear();
            Shop.Clear();
            InShopRoom = false;
            var candidates = PartySize < CardPool.MaxPartySize ? CardPool.AvailableRecruits(Members) : null;
            var fixedAlly = candidates?.Find(c => c.RecruitRecipe == Current.Ally);
            bool sure = Stage == 1 && Current.Layer == DungeonMap.FirstEventLayer || Current.Ally != null;   // 2층·동료가 정해진 방은 확정(정해진 동료가 이미 파티면 다른 동료)
            if (fixedAlly != null || candidates != null && candidates.Count > 0 && (sure || _rng.NextDouble() < EventAllyChance))
            {
                var card = fixedAlly ?? candidates[_rng.Next(candidates.Count)];
                Current.Ally = card.RecruitRecipe;   // GuestAlly: 첫 웨이브 몹 한가운데서 버티고, 이기면 WinRoom이 합류시킨다
                bool unlocked = MetaProgress.Current.MeetAlly(card.RecruitRecipe);
                LastMessage = unlocked ? "새 동료 해금" : "";
                Phase = RunPhase.InRoom;
                return;
            }
            RewardTaken = true;
            var ev = EventPool.Roll(this, _rng);
            if (ev != null)
            {
                PendingEvent = ev;   // 지도 위 모달(EventChoiceHud)에서 고른다. 고르기 전엔 지도를 못 누른다
                LastMessage = "";
                InventoryVersion++;
                return;
            }
            // 폴백 금화(임시값): 30 + 8×깊이 + 0~20, 처치 골드 배율(GoldMul) 적용
            int gold = (int)System.MathF.Round((30 + 8 * Depth + _rng.Next(0, 21)) * BuildModifiers(-1).GoldMul);
            Gold += gold;
            LastMessage = $"? 금화 +{gold}";
            InventoryVersion++;
        }

        /// <summary>그 파티원의 전투 후 회복 비율 = 난이도 기본값 + 유물·장비 보정(0~1).</summary>
        public float PostBattleHealRatio(int slot)
            => System.Math.Clamp(DifficultyTable.PostBattleHeal(Difficulty) + BuildModifiers(slot).PostBattleHealAdd, 0f, 1f);

        /// <summary>전투에서 이겼다. 파티 체력을 기억하고(살아 있는 파티원은 전투 후 회복) 정비로 간다(보스였으면 판 클리어).</summary>
        public void WinRoom(IReadOnlyList<float> partyHp)
        {
            PartyHp.Clear();
            PartyHp.AddRange(partyHp);
            for (int i = 0; i < PartyHp.Count; i++)
                PartyHp[i] = PartyHp[i] > 0f ? System.MathF.Min(1f, PartyHp[i] + PostBattleHealRatio(i))
                                             : ClearReviveRatio;   // 쓰러진 파티원은 방을 깨면 체력 조금으로 일어난다(강타가 치명적이라)

            LastHeal = DifficultyTable.PostBattleHeal(Difficulty) + BuildModifiers(-1).PostBattleHealAdd;   // 표시용(유물까지, 장비는 파티원별)
            Stats.Battles++;
            Stats.Rooms++;
            for (int i = 0; i < partyHp.Count; i++) if (partyHp[i] <= 0f) Stats.PartyDowns++;
            bool boss = Current != null && Current.Type == RoomType.Boss;
            Phase = boss && IsLastStage ? RunPhase.Cleared : RunPhase.Camp;
            if (boss && !IsLastStage) StageCleared = true;   // 정비 뒤 다음 스테이지
            if (boss) MetaProgress.Current.AddBossKill();      // 잼 슬롯 해금 기록
            if (Phase == RunPhase.Cleared) EndRun(true);

            // 동료 방: 같이 싸운 동료가 그대로 합류(체력 가득, 레벨은 파티에서 가장 낮은 레벨에 맞춤)
            var guest = GuestAlly;
            if (guest != null && Phase == RunPhase.Camp) Join(guest);

            // 전투 방을 깰 때마다 보상 카드 3장 중 1장(정예는 한 단계 좋게 + 아이템 카드, 보스는 희귀 이상 + 저주 칸). 마지막 보스는 판 클리어라 없음
            PendingRewards.Clear();
            if (Phase == RunPhase.Camp && Current != null)
            {
                bool elite = Current.Type == RoomType.Elite || ForcedEncounter == RoomType.Elite;   // 매복도 정예 보상
                int tier = System.Math.Min(RelicPool.MaxTier, RelicPool.Tier(this, stageReward: boss) + (elite ? 1 : 0) + RewardTierBonus);
                RollRewards(boss, elite, tier);
            }
            RewardTierBonus = 0;
            ForcedEncounter = null;
            RewardTaken = PendingRewards.Count == 0;
            InShopRoom = false;
            Shop.Clear();
            if (Phase == RunPhase.Camp && Current != null)
                Shop.AddRange(ShopPool.Generate(false, this, _rng));
        }

        /// <summary>
        /// 승리 보상 카드 3장(임시값). 일반: 유물 3. 정예: 유물 2 + 아이템 카드(희귀, Depth ≥ 8이면 영웅).
        /// 보스: 희귀 이상 유물 2 + 저주 유물 1(없으면 유물). 모자라면 일반 풀로 채운다(보스는 하한 티어 3).
        /// </summary>
        void RollRewards(bool boss, bool elite, int tier)
        {
            if (boss)
            {
                var a = RelicPool.RollRarity(this, ItemRarity.Rare, _rng);
                var b = RelicPool.RollRarity(this, ItemRarity.Rare, _rng, a != null ? new[] { a } : null);
                var ab = new List<CardDef>(2);
                if (a != null) ab.Add(a);
                if (b != null) ab.Add(b);
                var c = RelicPool.RollCursed(this, _rng) ?? RelicPool.RollRarity(this, ItemRarity.Rare, _rng, ab);
                foreach (var x in new[] { a, b, c }) if (x != null && !PendingRewards.Contains(x)) PendingRewards.Add(x);
                if (PendingRewards.Count < RelicPool.RewardChoices)
                    PendingRewards.AddRange(RelicPool.Roll(this, RelicPool.RewardChoices - PendingRewards.Count, System.Math.Max(tier, 3), _rng, PendingRewards));   // 보스 하한 티어 3
                return;
            }
            if (elite)
            {
                PendingRewards.AddRange(RelicPool.Roll(this, 2, tier, _rng));
                var item = ItemPool.RollRarity(_rng, Depth >= 8 ? ItemRarity.Epic : ItemRarity.Rare);
                if (item != null) PendingRewards.Add(CardDef.Item(item));
                if (PendingRewards.Count < RelicPool.RewardChoices)
                    PendingRewards.AddRange(RelicPool.Roll(this, RelicPool.RewardChoices - PendingRewards.Count, tier, _rng, PendingRewards));
                return;
            }
            PendingRewards.AddRange(RelicPool.Roll(this, RelicPool.RewardChoices, tier, _rng));
        }

        /// <summary>보상 건너뛰기 금화(임시값) = 상점 아이템 1개 값쯤.</summary>
        public int SkipGold() => 25 + 5 * Depth;

        /// <summary>보상 카드를 안 고르고 금화로 받는다.</summary>
        public void SkipReward()
        {
            if (RewardTaken || Phase != RunPhase.Camp) return;
            Gold += SkipGold();
            RewardTaken = true;
            InventoryVersion++;
        }

        /// <summary>상점 표시·차감 가격(상인의 장부 등 ShopPriceMul 반영, 최소 1).</summary>
        public int PriceOf(ShopOffer o) => System.Math.Max(1, (int)System.MathF.Round(o.Price * BuildModifiers(-1).ShopPriceMul));

        void Join(string recipe)
        {
            int lowest = int.MaxValue;
            for (int s = 0; s < Members.Count; s++) lowest = System.Math.Min(lowest, LevelOf(s));
            int slot = Members.Count;
            Members.Add(recipe);
            if (PartyHp.Count > 0) PartyHp.Add(1f);   // 비어 있으면 전원 가득으로 취급되므로 그대로
            Grow(slot);
            Levels[slot] = lowest == int.MaxValue ? 1 : lowest;
            Xp[slot] = 0;
            var card = CardPool.RecruitOf(recipe);
            if (card != null) Deck.Add(card);
            InventoryVersion++;
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
            if (o.Kind == ShopOfferKind.Card && Relics.Contains(o.Card)) return "이미 가진 유물입니다";
            if (o.Kind == ShopOfferKind.Revive && !AnyFallen()) return "쓰러진 파티원이 없습니다";
            if (o.Kind == ShopOfferKind.Heal && !AnyHurt()) return "다친 파티원이 없습니다";
            if (Gold < PriceOf(o)) return "골드가 부족합니다";
            return null;
        }

        /// <summary>산다. 아이템은 주운 아이템 칸으로(정비 화면에서 끌어서 넣는다). 못 사면 이유를 돌려준다.</summary>
        public string Buy(ShopOffer o)
        {
            var why = CannotBuy(o);
            if (why != null) return why;
            Gold -= PriceOf(o);
            switch (o.Kind)
            {
                case ShopOfferKind.Item:
                    Loot.Add(o.Item);
                    o.Sold = true;
                    break;
                case ShopOfferKind.Card:   // 유물
                    Relics.Add(o.Card);
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
                Gold = Gold, SealTier = SealTier, StageTier = StageTier, LastXp = LastXp, LastGold = LastGold, LastHeal = LastHeal, LastMessage = LastMessage ?? "",
                Seed = Seed, PlaySeconds = PlaySeconds, Stats = Stats.ToSave(),
                ForcedEncounter = ForcedEncounter.HasValue ? (int)ForcedEncounter.Value : -1, RewardTierBonus = RewardTierBonus, InRestRoom = InRestRoom
            };
            foreach (var row in Map.Layers)
                foreach (var n in row)
                    s.Nodes.Add(new NodeSave { Layer = n.Layer, Index = n.Index, Type = (int)n.Type, Visited = n.Visited, Ally = n.Ally ?? "", Next = new List<int>(n.Next) });
            s.Members.AddRange(Members);
            s.Formation.AddRange(Formation);
            s.PartyHp.AddRange(PartyHp);
            s.Levels.AddRange(Levels);
            s.Xp.AddRange(Xp);
            s.LastLevelUps.AddRange(LastLevelUps);
            for (int slot = 0; slot < Traits.Count; slot++)
                foreach (var id in Traits[slot]) s.Traits.Add($"{slot}:{id}");
            foreach (var p in PendingTraits) s.PendingTraits.Add($"{p.Slot}:{p.Level}:{string.Join(",", p.Options)}");
            for (int slot = 0; slot < Gear.Count; slot++)
                foreach (var p in Gear[slot].Items) s.Items.Add(new ItemSave { Owner = slot, Item = p.Def.Index, Level = p.Def.Level, X = p.X, Y = p.Y, Rotated = p.Rotated });
            foreach (var p in Stash.Items) s.Items.Add(new ItemSave { Owner = -1, Item = p.Def.Index, Level = p.Def.Level, X = p.X, Y = p.Y, Rotated = p.Rotated });
            foreach (var d in Loot) { s.Loot.Add(d.Index); s.LootLevels.Add(d.Level); }
            foreach (var c in Deck) s.Deck.Add(c.Id);
            foreach (var c in Relics) s.Relics.Add(c.Id);
            foreach (var c in PendingRewards) s.PendingRewards.Add(c.Id);
            foreach (var o in Shop)
                s.Shop.Add(new OfferSave { Kind = (int)o.Kind, Item = o.Item?.Index ?? -1, Card = o.Card?.Id ?? "", Price = o.Price, Sold = o.Sold });
            SaveEvents(s);
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
                var node = new MapNode { Layer = ns.Layer, Index = ns.Index, Type = (RoomType)ns.Type, Visited = ns.Visited, Ally = string.IsNullOrEmpty(ns.Ally) ? null : ns.Ally };
                if (ns.Next != null) node.Next.AddRange(ns.Next);
                map.Layers[ns.Layer].Add(node);
            }
            foreach (var row in map.Layers) row.Sort((a, b) => a.Index.CompareTo(b.Index));

            var run = new RunData(s.Seed != 0 ? s.Seed : System.Environment.TickCount, (Difficulty)s.Difficulty, map)
            {
                Stage = System.Math.Max(1, s.Stage),
                StageCleared = s.StageCleared,
                Phase = (RunPhase)s.Phase,
                CanReturnToCamp = s.CanReturnToCamp,
                InShopRoom = s.InShopRoom,
                InRestRoom = s.InRestRoom,
                RewardTaken = s.RewardTaken,
                Gold = s.Gold, SealTier = System.Math.Clamp(s.SealTier, 0, SealStone.MaxTier), StageTier = System.Math.Clamp(s.StageTier, 0, DungeonTier.Count - 1), LastXp = s.LastXp, LastGold = s.LastGold, LastHeal = s.LastHeal,
                LastMessage = s.LastMessage ?? "",
                PlaySeconds = s.PlaySeconds, RewardTierBonus = s.RewardTierBonus,
                ForcedEncounter = s.ForcedEncounter >= 0 ? (RoomType?)(RoomType)s.ForcedEncounter : null
            };
            run.Stats.CopyFrom(s.Stats);   // 옛 저장(null)이면 그대로 0
            if (!run.CanSave) run.Phase = RunPhase.ChoosingRoom;
            if (s.CurrentLayer >= 0 && s.CurrentLayer < map.LayerCount)
                run.Current = map.Layers[s.CurrentLayer].Find(n => n.Index == s.CurrentIndex);

            run.Members.Clear();
            run.Members.AddRange(s.Members);
            if (s.Formation != null) run.Formation.AddRange(s.Formation);
            run.PartyHp.AddRange(s.PartyHp);
            run.Levels.AddRange(s.Levels);
            run.Xp.AddRange(s.Xp);
            run.LastLevelUps.AddRange(s.LastLevelUps);
            if (s.Traits != null)
                foreach (var t in s.Traits)
                {
                    var parts = t.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int slot) && TraitPool.Find(parts[1]) != null) run.TraitsOf(slot).Add(parts[1]);
                }
            if (s.PendingTraits != null)
                foreach (var t in s.PendingTraits)
                {
                    var parts = t.Split(':');
                    if (parts.Length != 3 || !int.TryParse(parts[0], out int slot) || !int.TryParse(parts[1], out int lv)) continue;
                    var pick = new TraitPick { Slot = slot, Level = lv };
                    foreach (var id in parts[2].Split(',')) if (TraitPool.Find(id) != null) pick.Options.Add(id);
                    if (pick.Options.Count > 0) run.PendingTraits.Add(pick);
                }
            foreach (var it in s.Items)
            {
                var def = ItemEnhance.Of(ItemPool.Get(it.Item), it.Level);
                if (def == null) continue;
                var inv = it.Owner >= 0 ? run.GearOf(it.Owner) : run.Stash;
                if (inv.Place(def, it.X, it.Y, it.Rotated) == null) run.Loot.Add(def);   // 칸 규칙이 바뀌었으면 주운 아이템 칸으로
            }
            for (int i = 0; i < s.Loot.Count; i++)
            {
                int level = s.LootLevels != null && i < s.LootLevels.Count ? s.LootLevels[i] : 0;
                var def = ItemEnhance.Of(ItemPool.Get(s.Loot[i]), level);
                if (def != null) run.Loot.Add(def);
            }
            foreach (var id in s.Relics) { var c = RelicPool.Find(id); if (c != null && !run.Relics.Contains(c)) run.Relics.Add(c); }
            foreach (var id in s.Deck)
            {
                var c = CardPool.Find(id);
                if (c == null) continue;
                // 옛 저장: 덱의 패시브 카드는 같은 Id의 유물로 옮긴다(겹치던 건 하나만)
                if (c.Kind == CardKind.Relic) { if (!run.Relics.Contains(c)) run.Relics.Add(c); }
                else run.Deck.Add(c);
            }
            foreach (var id in s.PendingRewards) { var c = CardPool.Find(id); if (c != null) run.PendingRewards.Add(c); }
            foreach (var os in s.Shop)
            {
                var o = new ShopOffer { Kind = (ShopOfferKind)os.Kind, Item = ItemPool.Get(os.Item), Card = CardPool.Find(os.Card), Price = os.Price, Sold = os.Sold };
                if (o.Kind == ShopOfferKind.Item && o.Item == null) continue;
                if (o.Kind is ShopOfferKind.Card or ShopOfferKind.Recruit && o.Card == null) continue;
                run.Shop.Add(o);
            }
            run.LoadEvents(s);
            return run;
        }

        /// <summary>보상 하나를 고른다. 동료 영입이면 파티에 합류(체력 가득), 유물이면 유물 칸에 넣는다.</summary>
        public void TakeReward(CardDef card)
        {
            if (RewardTaken || Phase != RunPhase.Camp || !PendingRewards.Contains(card)) return;
            RewardTaken = true;
            if (card.Kind == CardKind.Recruit)
            {
                if (PartySize < CardPool.MaxPartySize && !Members.Contains(card.RecruitRecipe)) Join(card.RecruitRecipe);   // 덱 기록도 Join이
                return;
            }
            if (card.Kind == CardKind.Relic)
            {
                if (!Relics.Contains(card)) Relics.Add(card);
                return;
            }
            if (card.Kind == CardKind.Gold) { Gold += card.Amount; InventoryVersion++; return; }
            if (card.Kind == CardKind.Item)
            {
                var def = ItemPool.Get(card.ItemIndex);
                if (def != null) Loot.Add(def);   // 주운 아이템 칸으로(정비 화면에서 가방에 넣는다)
                InventoryVersion++;
                return;
            }
            Deck.Add(card);
        }

        /// <summary>정비 화면에서 지도로. 방을 고르기 전까진 ReturnToCamp로 다시 정비할 수 있다.</summary>
        public void LeaveCamp()
        {
            if (Phase != RunPhase.Camp) return;
            Phase = RunPhase.ChoosingRoom;
            CanReturnToCamp = !InShopRoom && !InRestRoom;   // 상점·휴식 방에서 나오면 그 정비로 되돌아가지 않는다
            InShopRoom = false;
            InRestRoom = false;
            if (StageCleared)
            {
                // 다음 스테이지: 새 지도 첫 층부터
                StageCleared = false;
                Stage++;
                Map = DungeonMap.Generate(_rng.Next());   // 2스테이지부터는 처음 하는 판도 갈림길
                DungeonMap.PlaceAllies(Map, Stage);
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

        /// <summary>이번 판에 얻은 잼 Id(처치 드랍 + 판 끝 보상). 잼 획득 연출(GemRevealHud)이 늘어난 만큼 하나씩 보여 준다.</summary>
        public readonly List<string> GemsFound = new();
        bool _runEnded;

        /// <summary>잼 하나를 얻는다(판이 끝나도 남는다). specific이 없으면 무작위(보스 잼 제외).</summary>
        public GemDef FindGem(GemDef specific = null)
        {
            var gem = specific ?? GemPool.Roll(_rng);
            MetaProgress.Current.AddGem(gem.Id);
            GemsFound.Add(gem.Id);
            return gem;
        }

        /// <summary>판 끝 보상(클리어든 전멸이든 한 번): 잼 1 + 지나온 깊이 3마다 1 + 클리어 3(임시값).</summary>
        void EndRun(bool cleared)
        {
            if (_runEnded) return;
            _runEnded = true;
            int gems = 1 + Depth / 3 + (cleared ? 3 : 0) + SealStone.EndGems(SealTier) + DungeonTier.EndGems(StageTier);
            for (int i = 0; i < gems; i++) FindGem();
            MetaProgress.Current.EndRun(cleared);
            OnRunEnded(cleared);
        }
    }

    public static class RunState
    {
        static RunData _current;

        public static RunData Current => _current ??= new RunData(System.Environment.TickCount);

        /// <summary>새 판. seed가 0이면 시각으로(기존 호출 호환), 아니면 그 시드로(재도전·시드 입력).</summary>
        public static void StartNew(Difficulty difficulty = Difficulty.Normal, string starter = null, int seed = 0)
            => _current = new RunData(seed == 0 ? System.Environment.TickCount : seed, difficulty, starter);

        /// <summary>이어하기: 저장에서 되살린 판으로 바꾼다.</summary>
        public static void Continue(RunData run) => _current = run;
    }
}
