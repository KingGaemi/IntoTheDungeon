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
    /// 상점 가격·품목(임시값). 정비 화면엔 작은 상점(아이템 3 + 카드 1 + 회복),
    /// 지도의 상점 방엔 큰 상점(아이템 5 + 카드 2 + 영입 + 부활 + 회복, 20% 할인·좋은 등급이 잘 나옴).
    /// 아이템을 팔면 원래 값의 절반.
    /// </summary>
    public static class ShopPool
    {
        public const int CardPrice = 60, RecruitPrice = 150, RevivePrice = 50, HealPrice = 30;
        public const float RoomDiscount = 0.8f;
        public const float HealRatio = 0.3f;     // 회복: 살아 있는 파티원 전원 최대 체력의 30%
        public const float ReviveRatio = 0.5f;   // 부활: 쓰러진 파티원 1명을 체력 50%로

        public static int ItemPrice(ItemRarity r) => r switch
        {
            ItemRarity.Uncommon => 45,
            ItemRarity.Rare => 90,
            ItemRarity.Epic => 170,
            _ => 20
        };

        public static int SellPrice(ItemDef def) => ItemPrice(def.Rarity) / 2;

        public static List<ShopOffer> Generate(bool room, int partySize, int depth, System.Random rng)
        {
            var offers = new List<ShopOffer>();
            float mul = room ? RoomDiscount : 1f;
            int Price(int p) => System.Math.Max(1, (int)System.MathF.Round(p * mul));

            int items = room ? 5 : 3;
            float rareBonus = (room ? 0.3f : 0f) + 0.05f * depth;
            for (int i = 0; i < items; i++)
            {
                var def = ItemPool.Roll(rng, rareBonus);
                offers.Add(new ShopOffer { Kind = ShopOfferKind.Item, Item = def, Price = Price(ItemPrice(def.Rarity)) });
            }

            var pool = new List<CardDef>(CardPool.Passives);
            int cards = room ? 2 : 1;
            for (int i = 0; i < cards && pool.Count > 0; i++)
            {
                int k = rng.Next(pool.Count);
                offers.Add(new ShopOffer { Kind = ShopOfferKind.Card, Card = pool[k], Price = Price(CardPrice) });
                pool.RemoveAt(k);
            }

            if (room && partySize < CardPool.MaxPartySize)
                offers.Add(new ShopOffer { Kind = ShopOfferKind.Recruit, Card = CardPool.Recruits[rng.Next(CardPool.Recruits.Length)], Price = Price(RecruitPrice) });
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
            ShopOfferKind.Card => "패시브 카드",
            ShopOfferKind.Recruit => "동료 영입",
            ShopOfferKind.Revive => "부활",
            _ => "회복"
        };
    }
}
