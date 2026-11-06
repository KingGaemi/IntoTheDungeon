using System;
using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 「?」방·휴식 방 선택지 하나. 화면은 아이콘·숫자 토큰(Cost/Gain)으로만 읽히고, 글은 Tip(툴팁)에만.
    /// 토큰 문법: "종류[.등급]:값", 여러 개는 ';'로. 종류 = hp heal revive gold relic curse item xp gem trait fight dice.
    /// 예: "hp:-30%" "gold:-60" "relic.rare:+1" "item.epic:+1" "curse:+1" "gem:+2" "xp:+50%" "trait:+1" "fight:정예" "revive:60%" "heal:+25%"
    /// </summary>
    public sealed class EventOption
    {
        public string Id, Label;                     // Label 한두 단어("제물", "도박")
        public string Icon;                          // 카드 큰 아이콘 토큰(hp gold dice skull shrine heal revive item xp gem trait atk pass relic)
        public Func<RunData, string> Cost, Gain;     // 비용·보상 토큰(금액이 깊이·소지금에 따라 달라 함수). null = 없음
        public string Tip = "";                      // 툴팁 한 줄(설명은 여기만)
        public Func<RunData, bool> Show;             // null = 항상 보임. false면 카드를 안 그린다(없는 유물 등)
        public Func<RunData, bool> Can;              // null = 항상 가능. false면 흐리게·안 눌림
        public Action<RunData> Apply;

        public bool Visible(RunData run) => Show == null || Show(run);
        public bool Usable(RunData run) => Can == null || Can(run);
        public string CostOf(RunData run) => Cost?.Invoke(run) ?? "";
        public string GainOf(RunData run) => Gain?.Invoke(run) ?? "";
    }

    /// <summary>이벤트 하나(제목 + 선택지 ≤ 4). MinDepth·Available로 나올 수 있는 때를 거른다.</summary>
    public sealed class EventDef
    {
        public string Id, Title;
        public int MinDepth;
        public Func<RunData, bool> Available;        // null = 항상(이번 판 상태로 나올 수 있는지)
        public readonly List<EventOption> Options = new();
    }

    /// <summary>
    /// 「?」방 이벤트 9종 + 휴식 3택(임시값). 같은 판에 같은 이벤트는 한 번(RunData.SeenEvents).
    /// 다른 트랙 유물(shrinemark)·저주 유물이 없으면 그 선택지는 숨기거나 생략한다.
    /// </summary>
    public static class EventPool
    {
        public const string RestId = "rest";

        // ───── 수치(전부 임시값) ─────
        public const float AltarHpCost = 0.30f;                                      // 제단 ① 살아 있는 전원 체력 −30%
        public const int AltarGoldBase = 40, AltarGoldPerDepth = 6;                  // 제단 ② 금 −(40 + 6×깊이)
        public const int GamblerMinGold = 20, GamblerItemGold = 30;                  // 도박꾼: 걸 수 있는 최소 금, 아이템 값
        public const double GamblerWinChance = 0.5;                                  // 도박 성공 확률
        public const float GamblerWinMul = 2.5f, GamblerItemBonus = 0.5f;            // 성공 시 건 금 ×2.5, 아이템 등급 보너스
        public const int ChestGoldBase = 20, ChestGoldPerDepth = 4;                  // 저주 상자 ② 금 +(20 + 4×깊이)
        public const float SpringHeal = 0.25f, SpringRevive = 0.6f;                  // 샘물: 전원 +25%, 쓰러진 1명 60%로
        public const float TrainerHpCost = 0.2f;                                     // 교관 ① 그 파티원 체력 −20%
        public const int TrainerGold = 50;                                           // 교관 ② 금 −50 → 전원 다음 레벨 경험치의 절반
        public const int GhostCurseGold = 120, GhostSellGold = 80;                   // 유령 상인: 저주 받기 +120, 유물 팔기 +80
        public const int ShrineAnvilGold = 80;                                       // 저지의 사당 ② 모루 값
        public const int AmbushGold = 50, AmbushMinDepth = 3;                        // 매복: 싸우면 금 +50(+보상 티어 1), 3층부터
        public const float AmbushFleeHp = 0.15f;                                     // 도망: 살아 있는 전원 −15%
        public const int WellMinDepth = 2, WellMinGold = 10, WellAllMinGold = 100;   // 우물: 2층부터, 동전 최소 10, 전부는 100 이상
        public const float RestTrainHeal = 0.1f;                                     // 휴식 ② 단련한 파티원 +10%
        public const string ShrineMarkId = "shrinemark", AnvilId = "anvil";          // relics-traits 유물 Id(없으면 선택지 숨김)

        static int AltarGold(RunData r) => AltarGoldBase + AltarGoldPerDepth * r.Depth;
        static int ChestGold(RunData r) => ChestGoldBase + ChestGoldPerDepth * r.Depth;
        static int WellCoin(RunData r) => Math.Max(WellMinGold, r.Gold / 10);
        static int GamblerBet(RunData r) => r.Gold / 2;
        static int GamblerPrize(RunData r) => (int)MathF.Round(GamblerBet(r) * GamblerWinMul);
        static string Pct(float f) => $"{Math.Round(f * 100f)}%";

        /// <summary>끼고 있는 첫 잼 Id(없으면 null).</summary>
        static string FirstEquippedGem()
        {
            var meta = MetaProgress.Current;
            for (int i = 0; i < meta.SlotCount; i++) { var id = meta.InSlot(i); if (id != null) return id; }
            return null;
        }

        static string RarityToken(CardDef c) => c == null ? "relic:+1" : c.Rarity switch
        {
            ItemRarity.Epic => "relic.epic:+1",
            ItemRarity.Rare => "relic.rare:+1",
            ItemRarity.Uncommon => "relic.uncommon:+1",
            _ => "relic:+1",
        };

        static EventDef Def(string id, string title, int minDepth, Func<RunData, bool> available, params EventOption[] options)
        {
            var d = new EventDef { Id = id, Title = title, MinDepth = minDepth, Available = available };
            d.Options.AddRange(options);
            return d;
        }

        static EventOption Opt(string id, string label, string icon, Func<RunData, string> cost, Func<RunData, string> gain, string tip, Action<RunData> apply,
                               Func<RunData, bool> can = null, Func<RunData, bool> show = null)
            => new() { Id = id, Label = label, Icon = icon, Cost = cost, Gain = gain, Tip = tip ?? "", Apply = apply, Can = can, Show = show };

        static EventOption Pass() => Opt("pass", "지나감", "pass", null, null, "아무것도 하지 않고 지나간다.", null);

        static Func<RunData, string> T(string token) => _ => token;   // 고정 토큰

        public static readonly EventDef[] All =
        {
            // ① 제단: 피 또는 금으로 유물
            Def("altar", "제단", 0, r => RunData.HasPlainRelicLeft(r),
                Opt("blood", "제물", "hp", T($"hp:-{Pct(AltarHpCost)}"), T("relic.rare:+1"),
                    "살아 있는 전원의 체력 30%를 바치고 희귀 이상 유물 하나를 받는다.",
                    r => { r.HurtLiving(AltarHpCost); r.GiveRelic(RelicPool.RollRarity(r, ItemRarity.Rare, r.Rng)); }),
                Opt("tithe", "헌금", "gold", r => $"gold:-{AltarGold(r)}", T("relic.uncommon:+1"),
                    "금화를 바치고 고급 이상 유물 하나를 받는다.",
                    r => { r.Gold -= AltarGold(r); r.GiveRelic(RelicPool.RollRarity(r, ItemRarity.Uncommon, r.Rng)); },
                    can: r => r.Gold >= AltarGold(r)),
                Pass()),

            // ② 도박꾼: 금 절반을 걸거나, 금 30으로 아이템
            Def("gambler", "도박꾼", 0, r => r.Gold >= GamblerMinGold,
                Opt("bet", "도박", "dice", r => $"gold:-{GamblerBet(r)}", r => $"dice:50%;gold:+{GamblerPrize(r)}",
                    "금화 절반을 건다. 절반의 확률로 건 금의 2.5배를 돌려받고, 아니면 잃는다.",
                    r =>
                    {
                        int bet = GamblerBet(r), prize = GamblerPrize(r);
                        r.Gold -= bet;
                        bool win = r.Rng.NextDouble() < GamblerWinChance;
                        if (win) r.Gold += prize;
                        r.LastMessage += win ? $" +{prize}" : $" -{bet}";
                    },
                    can: r => r.Gold >= GamblerMinGold),
                Opt("buy", "구매", "item", T($"gold:-{GamblerItemGold}"), T("item:+1"),
                    "금화 30으로 아이템 하나를 산다(등급은 운).",
                    r => { r.Gold -= GamblerItemGold; var item = ItemPool.Roll(r.Rng, GamblerItemBonus); if (item != null) { r.Loot.Add(item); r.Stats.ItemsFound++; } },
                    can: r => r.Gold >= GamblerItemGold),
                Pass()),

            // ③ 저주 상자: 영웅 아이템 + 저주 유물, 또는 부숴서 금
            Def("cursed_chest", "저주 상자", 0, null,
                Opt("open", "열기", "skull", r => RunData.HasCursedLeft(r) ? "curse:+1" : "", T("item.epic:+1"),
                    "영웅 아이템 하나를 꺼낸다. 저주 유물 하나가 따라붙는다.",
                    r =>
                    {
                        var item = ItemPool.RollRarity(r.Rng, ItemRarity.Epic);
                        if (item != null) { r.Loot.Add(item); r.Stats.ItemsFound++; }
                        r.TakeCursedRelic();   // 저주 유물이 없으면 생략
                    }),
                Opt("smash", "부수기", "gold", null, r => $"gold:+{ChestGold(r)}",
                    "상자를 부수고 금화만 챙긴다.",
                    r => r.Gold += ChestGold(r)),
                Pass()),

            // ④ 샘물: 전원 회복 또는 쓰러진 1명 일으키기
            Def("spring", "샘물", 0, r => r.AnyHurtOrFallen(),
                Opt("drink", "마시기", "heal", null, T($"heal:+{Pct(SpringHeal)}"),
                    "살아 있는 전원의 체력을 25% 회복한다.",
                    r => r.HealLiving(SpringHeal)),
                Opt("raise", "일으키기", "revive", null, T($"revive:{Pct(SpringRevive)}"),
                    "쓰러진 파티원 한 명(앞 슬롯)을 체력 60%로 일으킨다.",
                    r => r.ReviveFirstFallen(SpringRevive),
                    can: r => r.AnyFallenSlot()),
                Pass()),

            // ⑤ 교관: 체력으로 특성, 금으로 경험치
            Def("trainer", "교관", 0, null,
                Opt("drill", "수련", "trait", T($"hp:-{Pct(TrainerHpCost)}"), T("trait:+1"),
                    "레벨이 가장 낮은 파티원이 체력 20%를 내고 특성 하나를 바로 고른다.",
                    r => { int s = r.LowestLevelLivingSlot(); if (s >= 0 && r.OfferTrait(s)) r.HurtSlot(s, TrainerHpCost); },
                    can: r => r.LowestLevelLivingSlot() >= 0),
                Opt("lesson", "수업", "xp", T($"gold:-{TrainerGold}"), T("xp:+50%"),
                    "금화 50으로 전원이 다음 레벨까지 필요한 경험치의 절반을 얻는다.",
                    r => { r.Gold -= TrainerGold; for (int s = 0; s < r.PartySize; s++) r.AddXp(s, RunData.XpToNext(r.LevelOf(s)) / 2); },
                    can: r => r.Gold >= TrainerGold),
                Pass()),

            // ⑥ 유령 상인: 저주를 사 주거나, 유물을 팔거나
            Def("ghost_merchant", "유령 상인", 0, r => RunData.HasCursedLeft(r) || r.LastPlainRelicIndex() >= 0,
                Opt("curse", "저주", "skull", T("curse:+1"), T($"gold:+{GhostCurseGold}"),
                    "저주 유물 하나를 받고 금화 120을 받는다.",
                    r => { if (r.TakeCursedRelic() != null) r.Gold += GhostCurseGold; },
                    can: r => RunData.HasCursedLeft(r)),
                Opt("sell", "팔기", "relic", T("relic:-1"), T($"gold:+{GhostSellGold}"),
                    "가장 최근에 얻은 (저주 아닌) 유물 하나를 팔아 금화 80을 받는다.",
                    r => { int i = r.LastPlainRelicIndex(); if (i >= 0) { r.Relics.RemoveAt(i); r.Gold += GhostSellGold; } },
                    can: r => r.LastPlainRelicIndex() >= 0),
                Pass()),

            // ⑦ 저지의 사당: 표식(저지 +3, 최대 체력 ×0.9) 또는 금으로 모루
            Def("shrine", "저지의 사당", 0,
                r => RelicPool.Find(ShrineMarkId) != null && !r.HasRelic(ShrineMarkId) || RelicPool.Find(AnvilId) != null && !r.HasRelic(AnvilId),
                Opt("mark", "표식", "shrine", null, r => RarityToken(RelicPool.Find(ShrineMarkId)),
                    "사당의 표식을 받는다: 파티 필살기 저지력 +3, 최대 체력 ×0.9.",
                    r => r.GiveRelic(RelicPool.Find(ShrineMarkId)),
                    show: r => RelicPool.Find(ShrineMarkId) != null && !r.HasRelic(ShrineMarkId)),
                Opt("anvil", "모루", "relic", T($"gold:-{ShrineAnvilGold}"), r => RarityToken(RelicPool.Find(AnvilId)),
                    "금화 80으로 저지의 모루(파티 필살기 저지력 +5)를 산다.",
                    r => { r.Gold -= ShrineAnvilGold; r.GiveRelic(RelicPool.Find(AnvilId)); },
                    can: r => r.Gold >= ShrineAnvilGold,
                    show: r => RelicPool.Find(AnvilId) != null && !r.HasRelic(AnvilId)),
                Pass()),

            // ⑧ 매복: 정예 구성과 싸우거나(금 +50, 보상 티어 +1), 체력을 내고 도망
            Def("ambush", "매복", AmbushMinDepth, null,
                Opt("fight", "싸운다", "atk", T("fight:정예"), T($"gold:+{AmbushGold}"),
                    "정예 구성과 싸운다. 이기면 금화 50과 한 단계 좋은 유물 보상.",
                    r => { r.ForcedEncounter = RoomType.Elite; r.RewardTierBonus = 1; r.Gold += AmbushGold; r.StartForcedBattle(); }),
                Opt("flee", "도망", "pass", T($"hp:-{Pct(AmbushFleeHp)}"), null,
                    "살아 있는 전원이 체력 15%를 잃고 빠져나간다.",
                    r => r.HurtLiving(AmbushFleeHp))),

            // ⑨ 소원의 우물: 동전 하나로 잼, 전부 던지면 잼 2 + 유물
            Def("well", "소원의 우물", WellMinDepth, r => r.Gold >= WellMinGold,
                Opt("coin", "동전", "gold", r => $"gold:-{WellCoin(r)}", T("gem:+1"),
                    "금화 일부(10 이상, 소지금의 1/10)를 던지고 잼 하나를 얻는다.",
                    r => { r.Gold -= WellCoin(r); r.FindGem(); },
                    can: r => r.Gold >= WellMinGold),
                Opt("all", "전부", "gem", r => $"gold:-{r.Gold}", T("gem:+2;relic.rare:+1"),
                    "금화 전부를 던지고 잼 둘과 희귀 이상 유물 하나를 얻는다(100 이상).",
                    r => { r.Gold = 0; r.FindGem(); r.FindGem(); r.GiveRelic(RelicPool.RollRarity(r, ItemRarity.Rare, r.Rng)); },
                    can: r => r.Gold >= WellAllMinGold),
                Pass()),

            // 휴식 방 3택(Roll에서 제외, EnterRest가 직접 건다)
            Def(RestId, "휴식", 0, null,
                Opt("heal", "회복", "heal", null, T($"heal:+{Pct(RunData.RestHealRatio)}"),
                    "전원 체력 30% 회복. 쓰러진 파티원은 30%로 일어난다.",
                    r => r.HealLiving(RunData.RestHealRatio, fallenTo: RunData.RestHealRatio)),
                Opt("train", "단련", "trait", null, T($"trait:+1;hp:+{Pct(RestTrainHeal)}"),
                    "레벨이 가장 낮은 파티원이 특성 하나를 고르고 체력 10%를 회복한다.",
                    r => { int s = r.LowestLevelLivingSlot(); if (s >= 0 && r.OfferTrait(s)) r.HealSlot(s, RestTrainHeal); },
                    can: r => r.LowestLevelLivingSlot() >= 0),
                Opt("polish", "연마", "gem", null, T("gem:+1"),
                    "끼고 있는 첫 잼을 하나 더 얻는다.",
                    r => { var id = FirstEquippedGem(); var g = id != null ? GemPool.Find(id) : null; if (g != null) r.FindGem(g); },
                    can: _ => MetaProgress.Current.EquippedCount > 0)),
        };

        public static EventDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in All) if (e.Id == id) return e;
            return null;
        }

        /// <summary>이번 판에서 아직 안 본 이벤트 중 하나(휴식 제외, 깊이·조건 충족). 없으면 null(→ 금화 폴백).</summary>
        public static EventDef Roll(RunData run, Random rng)
        {
            var pool = new List<EventDef>();
            foreach (var e in All)
            {
                if (e.Id == RestId || e.MinDepth > run.Depth || run.SeenEvents.Contains(e.Id)) continue;
                if (e.Available != null && !e.Available(run)) continue;
                pool.Add(e);
            }
            return pool.Count > 0 ? pool[rng.Next(pool.Count)] : null;
        }
    }
}
