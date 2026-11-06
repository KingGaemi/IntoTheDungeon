using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 장비 강화: 같은 아이템을 겹쳐 놓으면 끌던 쪽(재료)을 먹여 놓인 쪽(베이스)의 강화 단계를 올린다.
    /// 첫 강화는 반드시 성공, 그다음부터 확률. 금화는 성공해도 들고, 실패하면 재료와 금화만 잃는다(베이스는 그대로).
    /// 강화된 아이템은 단계별 ItemDef 하나로 표현해 칸·주운 아이템·판매를 그대로 지나간다(Index는 원본과 같다).
    /// </summary>
    public static class ItemEnhance
    {
        public const int MaxLevel = 5;
        static readonly float[] Chances = { 1f, 0.7f, 0.5f, 0.35f, 0.25f };   // (임시값) 지금 단계 → 다음 단계 성공률
        const float HealPerLevel = 0.02f;                                     // (임시값) 회복 아이템 단계당 전투 후 회복

        static readonly Dictionary<(int index, int level), ItemDef> s_cache = new();

        public static float Chance(ItemDef target) => Chances[System.Math.Clamp(target.Level, 0, Chances.Length - 1)];

        /// <summary>(임시값) 금화: 판매가 기준으로 단계마다 늘어난다.</summary>
        public static int Cost(ItemDef target) => ShopPool.ItemPrice(target.Rarity) / 2 * (target.Level + 1);

        /// <summary>같은 아이템이고 베이스가 최대 단계 아래이며, 재료 단계가 베이스보다 높지 않을 때(높은 쪽을 실수로 먹이지 않게).</summary>
        public static bool CanCombine(ItemDef target, ItemDef material)
            => target != null && material != null && target.Id == material.Id
               && target.Level < MaxLevel && material.Level <= target.Level;

        /// <summary>그 아이템의 level 단계 정의(0 이하면 원본, def가 null이면 null).</summary>
        public static ItemDef Of(ItemDef def, int level)
        {
            if (def == null) return null;
            var root = def.Root;
            level = System.Math.Min(level, MaxLevel);
            if (level <= 0) return root;
            var key = (root.Index, level);
            if (s_cache.TryGetValue(key, out var made)) return made;

            var (add, label) = Bonus(root, level);
            var baseModify = root.Modify;
            made = new ItemDef(root, level, $"{root.Name} +{level}", $"{root.Description}\n<color=#FFD66B>강화: {label}</color>",
                               m => { baseModify?.Invoke(m); add(m); });
            s_cache[key] = made;
            return made;
        }

        /// <summary>그 단계의 강화 효과 글자(창에 미리 보여 준다).</summary>
        public static string BonusLabel(ItemDef def, int level) => Bonus(def.Root, level).label;

        // 아이템의 성격을 따라 오른다: 저지력 장비는 저지력, 스탯 장비는 가장 높은 스탯, 그 밖(음식)은 전투 후 회복
        static (System.Action<PartyModifiers> add, string label) Bonus(ItemDef root, int level)
        {
            var probe = new PartyModifiers();
            root.Modify?.Invoke(probe);
            if (probe.StopPowerAdd > 0)
                return (m => m.StopPowerAdd += level, $"필살기 저지력 +{level}");

            var s = probe.StatBonus;
            var top = System.Math.Max(System.Math.Max(s.Str, s.Agi), System.Math.Max(s.Int, s.Wil));
            if (top <= 0)
            {
                float heal = HealPerLevel * level;
                return (m => m.PostBattleHealAdd += heal, $"전투 후 회복 +{System.MathF.Round(heal * 100f)}%p");
            }

            bool str = s.Str == top, agi = s.Agi == top, intl = s.Int == top, wil = s.Wil == top;
            int tied = (str ? 1 : 0) + (agi ? 1 : 0) + (intl ? 1 : 0) + (wil ? 1 : 0);
            int amount = level * (tied == 1 && root.Rarity >= ItemRarity.Rare ? 2 : 1);   // (임시값) 희귀 이상은 단계당 2
            System.Action<PartyModifiers> add = m =>
            {
                if (str) m.StatBonus.Str += amount;
                if (agi) m.StatBonus.Agi += amount;
                if (intl) m.StatBonus.Int += amount;
                if (wil) m.StatBonus.Wil += amount;
            };
            if (tied == 4) return (add, $"모든 스탯 +{amount}");
            var names = new List<string>(4);
            if (str) names.Add("힘");
            if (agi) names.Add("민첩");
            if (intl) names.Add("지능");
            if (wil) names.Add("정신력");
            return (add, $"{string.Join("·", names)} +{amount}");
        }
    }

    /// <summary>아이템 하나의 자리: 격자 안이면 Inv·Placed, 주운 아이템 칸이면 Def만.</summary>
    public readonly struct ItemRef
    {
        public readonly Inventory Inv;
        public readonly PlacedItem Placed;
        public readonly ItemDef Def;

        public ItemRef(Inventory inv, PlacedItem placed, ItemDef def) { Inv = inv; Placed = placed; Def = def; }

        public ItemDef Item => Placed != null ? Placed.Def : Def;
    }

    public enum EnhanceResult { Invalid, NoGold, Success, Fail }

    public sealed partial class RunData
    {
        /// <summary>강화 시도: 금화와 재료는 결과와 상관없이 쓰이고, 성공하면 베이스가 한 단계 오른다.</summary>
        public EnhanceResult Enhance(ItemRef target, ItemRef material)
        {
            var baseItem = target.Item;
            if (!ItemEnhance.CanCombine(baseItem, material.Item) || !Holds(target) || !Holds(material)) return EnhanceResult.Invalid;
            int cost = ItemEnhance.Cost(baseItem);
            if (Gold < cost) return EnhanceResult.NoGold;

            Gold -= cost;
            if (material.Placed != null) material.Inv.Remove(material.Placed);
            else Loot.Remove(material.Def);

            bool success = _rng.NextDouble() < ItemEnhance.Chance(baseItem);
            if (success)
            {
                var up = ItemEnhance.Of(baseItem, baseItem.Level + 1);
                if (target.Placed != null) target.Placed.Def = up;
                else Loot[Loot.IndexOf(target.Def)] = up;
            }
            InventoryVersion++;
            return success ? EnhanceResult.Success : EnhanceResult.Fail;
        }

        bool Holds(ItemRef r) => r.Placed != null ? r.Inv != null && r.Inv.Items.Contains(r.Placed) : Loot.Contains(r.Def);
    }
}
