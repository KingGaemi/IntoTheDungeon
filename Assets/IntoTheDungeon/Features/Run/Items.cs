using System.Collections.Generic;
using IntoTheDungeon.Features.Stats;

namespace IntoTheDungeon.Features.Run
{
    public enum ItemRarity { Common, Uncommon, Rare, Epic }

    /// <summary>
    /// 아이템 정의. 파티원의 장비 칸(격자)에 놓으면 그 파티원에게 효과가 있다(보관함에선 효과 없음).
    /// 가치(등급)가 높을수록 가방 칸을 많이 차지한다(일반 1×1 · 고급 1×2 · 희귀 2×2 · 영웅 2×3).
    /// </summary>
    public sealed class ItemDef
    {
        public readonly int Index;          // ItemPool.All 안 순서(드랍 엔티티가 이 번호로 가리킴)
        public readonly string Id;
        public readonly string Name;
        public readonly ItemRarity Rarity;
        public readonly int W, H;           // 가방 칸(회전 전)
        public readonly string Description;
        public readonly System.Action<PartyModifiers> Modify;

        internal ItemDef(int index, string id, string name, ItemRarity rarity, string desc, System.Action<PartyModifiers> modify)
        {
            Index = index; Id = id; Name = name; Rarity = rarity; Description = desc; Modify = modify;
            (W, H) = SizeOf(rarity);
        }

        public static (int w, int h) SizeOf(ItemRarity r) => r switch
        {
            ItemRarity.Uncommon => (1, 2),
            ItemRarity.Rare => (2, 2),
            ItemRarity.Epic => (2, 3),
            _ => (1, 1)
        };

        public static string RarityLabel(ItemRarity r) => r switch
        {
            ItemRarity.Uncommon => "고급",
            ItemRarity.Rare => "희귀",
            ItemRarity.Epic => "영웅",
            _ => "일반"
        };
    }

    /// <summary>아이템 목록(임시값)과 드랍 등급 뽑기.</summary>
    public static class ItemPool
    {
        public static readonly List<ItemDef> All = new();

        static ItemPool()
        {
            // 아이콘(Gear·Foods 시트)에 맞춘 임시 목록: 등급이 오를수록 구리 → 철 → 금 → 청강 → 진홍
            Add("copper_dagger", "구리 단검", ItemRarity.Common, "힘 +1", m => m.StatBonus.Str += 1);
            Add("copper_greaves", "구리 각반", ItemRarity.Common, "민첩 +1", m => m.StatBonus.Agi += 1);
            Add("copper_rod", "구리 마법봉", ItemRarity.Common, "지능 +1", m => m.StatBonus.Int += 1);
            Add("copper_shield", "구리 원형 방패", ItemRarity.Common, "정신력 +1", m => m.StatBonus.Wil += 1);
            Add("roast_meat", "구운 고기", ItemRarity.Common, "전투 후 회복 +3%p", m => m.PostBattleHealAdd += 0.03f);

            Add("iron_sword", "철검", ItemRarity.Uncommon, "힘 +2", m => m.StatBonus.Str += 2);
            Add("iron_bow", "철 활", ItemRarity.Uncommon, "민첩 +2", m => m.StatBonus.Agi += 2);
            Add("iron_staff", "철 지팡이", ItemRarity.Uncommon, "지능 +2", m => m.StatBonus.Int += 2);
            Add("iron_helm", "철 투구", ItemRarity.Uncommon, "정신력 +2", m => m.StatBonus.Wil += 2);
            Add("herb_potion", "약초 물약", ItemRarity.Uncommon, "전투 후 회복 +5%p", m => m.PostBattleHealAdd += 0.05f);

            Add("gold_helm", "황금 기사 투구", ItemRarity.Rare, "힘 +3, 정신력 +2", m => { m.StatBonus.Str += 3; m.StatBonus.Wil += 2; });
            Add("gold_bow", "황금 장궁", ItemRarity.Rare, "민첩 +4", m => m.StatBonus.Agi += 4);
            Add("gold_staff", "황금 현자 지팡이", ItemRarity.Rare, "지능 +3, 필살기 재사용 대기 -10%", m => { m.StatBonus.Int += 3; m.SkillCooldownMul *= 0.9f; });
            Add("blue_sword", "청강 장검", ItemRarity.Rare, "힘 +3, 민첩 +2", m => { m.StatBonus.Str += 3; m.StatBonus.Agi += 2; });

            Add("red_shield", "진홍 연 방패", ItemRarity.Epic, "모든 스탯 +3", m => { m.StatBonus.Str += 3; m.StatBonus.Agi += 3; m.StatBonus.Int += 3; m.StatBonus.Wil += 3; });
            Add("red_greatsword", "진홍 대검", ItemRarity.Epic, "힘 +5, 민첩 +2", m => { m.StatBonus.Str += 5; m.StatBonus.Agi += 2; });
        }

        static void Add(string id, string name, ItemRarity r, string desc, System.Action<PartyModifiers> modify)
            => All.Add(new ItemDef(All.Count, id, name, r, desc, modify));

        public static ItemDef Get(int index) => index >= 0 && index < All.Count ? All[index] : null;

        /// <summary>등급을 뽑고 그 등급 안에서 하나. rareBonus가 클수록 높은 등급이 잘 나온다(정예·보스).</summary>
        public static ItemDef Roll(System.Random rng, float rareBonus)
        {
            // 기본 가중치: 일반 60 · 고급 28 · 희귀 10 · 영웅 2 (보너스만큼 위 등급으로 옮김)
            float common = System.MathF.Max(5f, 60f - 40f * rareBonus);
            float uncommon = 28f + 10f * rareBonus;
            float rare = 10f + 20f * rareBonus;
            float epic = 2f + 10f * rareBonus;
            double roll = rng.NextDouble() * (common + uncommon + rare + epic);
            var rarity = roll < common ? ItemRarity.Common
                       : roll < common + uncommon ? ItemRarity.Uncommon
                       : roll < common + uncommon + rare ? ItemRarity.Rare
                       : ItemRarity.Epic;

            var candidates = All.FindAll(d => d.Rarity == rarity);
            return candidates[rng.Next(candidates.Count)];
        }
    }

    /// <summary>가방 안에 놓인 아이템. (X, Y) = 차지한 칸 중 왼쪽 위 칸, Y는 아래로 증가.</summary>
    public sealed class PlacedItem
    {
        public ItemDef Def;
        public int X, Y;
        public bool Rotated;
        public int W => Rotated ? Def.H : Def.W;
        public int H => Rotated ? Def.W : Def.H;
    }

    /// <summary>격자 칸(장비 칸·보관함 공용): 아이템을 겹치지 않게 놓는다.</summary>
    public sealed class Inventory
    {
        public const int GearCols = 4, GearRows = 3;     // 파티원 장비 칸(여기 놓은 아이템이 그 파티원에게 적용)
        public const int StashCols = 6, StashRows = 3;   // 파티 보관함(효과 없음, 보관만)

        public readonly int Cols, Rows;
        public readonly List<PlacedItem> Items = new();

        public Inventory(int cols, int rows) { Cols = cols; Rows = rows; }

        public bool CanPlace(ItemDef def, int x, int y, bool rotated, PlacedItem ignore = null)
        {
            int w = rotated ? def.H : def.W, h = rotated ? def.W : def.H;
            if (x < 0 || y < 0 || x + w > Cols || y + h > Rows) return false;
            foreach (var p in Items)
            {
                if (p == ignore) continue;
                bool overlap = x < p.X + p.W && p.X < x + w && y < p.Y + p.H && p.Y < y + h;
                if (overlap) return false;
            }
            return true;
        }

        public PlacedItem Place(ItemDef def, int x, int y, bool rotated)
        {
            if (!CanPlace(def, x, y, rotated)) return null;
            var p = new PlacedItem { Def = def, X = x, Y = y, Rotated = rotated };
            Items.Add(p);
            return p;
        }

        public bool Move(PlacedItem item, int x, int y, bool rotated)
        {
            if (!CanPlace(item.Def, x, y, rotated, item)) return false;
            item.X = x; item.Y = y; item.Rotated = rotated;
            return true;
        }

        public void Remove(PlacedItem item) => Items.Remove(item);

        /// <summary>그 자리에 놓으면 겹치는 아이템들(격자 밖이면 null).</summary>
        public List<PlacedItem> Overlapping(ItemDef def, int x, int y, bool rotated, PlacedItem ignore = null)
        {
            int w = rotated ? def.H : def.W, h = rotated ? def.W : def.H;
            if (x < 0 || y < 0 || x + w > Cols || y + h > Rows) return null;
            var hits = new List<PlacedItem>();
            foreach (var p in Items)
                if (p != ignore && x < p.X + p.W && p.X < x + w && y < p.Y + p.H && p.Y < y + h) hits.Add(p);
            return hits;
        }

        /// <summary>
        /// 아이템을 놓는다: 먼저 (x, y)에 그 방향 → 반대 방향, 안 되면 왼쪽 위부터 빈자리(두 방향 모두). 놓은 칸, 못 놓으면 null.
        /// </summary>
        public PlacedItem PlaceNear(ItemDef def, int x, int y, bool rotated)
        {
            var p = Place(def, x, y, rotated) ?? Place(def, x, y, !rotated);
            for (int yy = 0; p == null && yy < Rows; yy++)
                for (int xx = 0; p == null && xx < Cols; xx++)
                    p = Place(def, xx, yy, rotated) ?? Place(def, xx, yy, !rotated);
            return p;
        }
    }
}
