using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    public enum ShopOfferKind { Item, Card, Recruit, Revive, Heal }

    /// <summary>상점 품목 하나. 아이템·카드·영입은 한 번 사면 품절, 부활은 쓰러진 파티원이 있는 동안 계속 살 수 있다.</summary>
    public sealed class ShopOffer
    {
        public ShopOfferKind Kind;
        public ItemDef Item;    // Item만
        public CardDef Card;    // Card·Recruit만
        public int Price;
        public bool Sold;
    }

    /// <summary>
    /// 상점 가격·품목(임시값). 정비 화면엔 작은 상점(아이템 3 + 유물 1 + 회복),
    /// 지도의 상점 방엔 큰 상점(아이템 5 + 유물 2 + 영입 + 부활 + 회복, 20% 할인·좋은 등급이 잘 나옴).
    /// 아이템을 팔면 원래 값의 절반.
    /// </summary>
    public static class ShopPool
    {
        public const int RecruitPrice = 150, RevivePrice = 50, HealPrice = 30;
        public const float RoomDiscount = 0.8f;
        public const float HealRatio = 0.3f;     // 회복: 살아 있는 파티원 전원 최대 체력의 30%
        public const float ReviveRatio = 0.5f;   // 부활: 쓰러진 파티원 1명을 체력 50%로
        public const double CursedShelfChance = 0.3;   // (임시값) 상점 방 진열대 한 칸이 저주 유물일 확률
        public const float CursedPriceMul = 0.5f;      // (임시값) 저주 유물 진열가 배율(반값)

        public static int ItemPrice(ItemRarity r) => r switch
        {
            ItemRarity.Uncommon => 45,
            ItemRarity.Rare => 90,
            ItemRarity.Epic => 170,
            _ => 20
        };

        public static int SellPrice(ItemDef def) => ItemPrice(def.Rarity) / 2 * (2 + def.Level) / 2;   // 강화 단계마다 원래 판매가의 절반씩 더(임시값)

        // ───────── 상점 티어(화면엔 안 보임) ─────────
        // 초반엔 살 수 없는 비싼 물건이 자리만 차지하지 않게, 진행도에 따라 상점 등급이 오른다.
        // 스테이지(2스테이지 +2) + 지도 절반 넘김(+1) + 파티 평균 레벨 5 이상(+1), 0~3.
        public const int MaxTier = 3;

        public static int Tier(RunData run)
        {
            int tier = (run.Stage - 1) * 2;
            if (run.CurrentLayer >= run.Map.LayerCount / 2) tier++;
            int sum = 0;
            for (int s = 0; s < run.PartySize; s++) sum += run.LevelOf(s);
            if (run.PartySize > 0 && sum >= 5 * run.PartySize) tier++;
            return System.Math.Clamp(tier, 0, MaxTier);
        }

        /// <summary>티어별 나올 수 있는 가장 높은 등급: 0 고급까지 · 1 희귀까지 · 2~ 영웅까지.</summary>
        public static ItemRarity MaxRarity(int tier) => tier switch { <= 0 => ItemRarity.Uncommon, 1 => ItemRarity.Rare, _ => ItemRarity.Epic };

        /// <summary>티어별 좋은 등급이 나올 확률 보정(상점 방은 여기에 +0.3).</summary>
        static float TierBonus(int tier) => tier switch { <= 0 => 0f, 1 => 0.1f, 2 => 0.25f, _ => 0.45f };

        public static List<ShopOffer> Generate(bool room, RunData run, System.Random rng)
        {
            int tier = Tier(run);
            var members = run.Members;
            var offers = new List<ShopOffer>();
            float mul = room ? RoomDiscount : 1f;
            int Price(int p) => System.Math.Max(1, (int)System.MathF.Round(p * mul));

            int items = room ? 5 : 3;
            float rareBonus = (room ? 0.3f : 0f) + TierBonus(tier);
            var maxRarity = MaxRarity(tier);
            for (int i = 0; i < items; i++)
            {
                var def = ItemPool.Roll(rng, rareBonus, maxRarity);
                offers.Add(new ShopOffer { Kind = ShopOfferKind.Item, Item = def, Price = Price(ItemPrice(def.Rarity)) });
            }

            // 유물 진열대: 정비 상인 1개 · 상점 방 2개(안 가진 일반 유물, 등급별 가격 RelicPool.Price에 방 할인).
            // 상점 방에선 CursedShelfChance로 한 칸이 저주 유물 반값(저주가 다 떨어졌으면 일반).
            int relics = room ? 2 : 1;
            CardDef cursed = room && rng.NextDouble() < CursedShelfChance ? RelicPool.RollCursed(run, rng) : null;
            // 이번 보상 카드(PendingRewards)에 나온 유물은 진열대에서 뺀다(같은 유물이 두 곳에 보이지 않게). 풀이 비면 빈 리스트
            var shelf = RelicPool.Roll(run, cursed != null ? relics - 1 : relics, RelicPool.Tier(run, false), rng, run.PendingRewards);
            foreach (var card in shelf)
                offers.Add(new ShopOffer { Kind = ShopOfferKind.Card, Card = card, Price = Price(RelicPool.Price(card.Rarity)) });
            if (cursed != null)
                offers.Add(new ShopOffer { Kind = ShopOfferKind.Card, Card = cursed, Price = System.Math.Max(1, (int)System.MathF.Round(Price(RelicPool.Price(cursed.Rarity)) * CursedPriceMul)) });

            // 동료는 상점에서 팔지 않는다(지도의 동료 방에서 만나 합류)
            if (room)
                offers.Add(new ShopOffer { Kind = ShopOfferKind.Revive, Price = Price(RevivePrice) });
            offers.Add(new ShopOffer { Kind = ShopOfferKind.Heal, Price = Price(HealPrice) });
            return offers;
        }

        public static string Title(ShopOffer o) => o.Kind switch
        {
            ShopOfferKind.Item => o.Item.Name,
            ShopOfferKind.Card or ShopOfferKind.Recruit => o.Card.Title,
            ShopOfferKind.Revive => "파티원 부활",
            _ => "치료"
        };

        public static string Description(ShopOffer o) => o.Kind switch
        {
            ShopOfferKind.Item => $"{o.Item.Description}\n({o.Item.W}×{o.Item.H}칸)",
            ShopOfferKind.Card or ShopOfferKind.Recruit => o.Card.Description,
            ShopOfferKind.Revive => $"쓰러진 파티원 1명을 체력 {ReviveRatio * 100f:0}%로 일으킴",
            _ => $"살아 있는 파티원 전원 체력 +{HealRatio * 100f:0}%"
        };

        public static string KindLabel(ShopOffer o) => o.Kind switch
        {
            ShopOfferKind.Item => $"아이템 · {ItemDef.RarityLabel(o.Item.Rarity)}",
            ShopOfferKind.Card => $"유물 · {ItemDef.RarityLabel(o.Card.Rarity)}",
            ShopOfferKind.Recruit => "동료 영입",
            ShopOfferKind.Revive => "부활",
            _ => "회복"
        };
    }
}
