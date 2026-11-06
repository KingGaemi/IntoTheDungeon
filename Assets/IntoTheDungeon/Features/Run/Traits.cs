using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>레벨업 특성 하나. 그 파티원에게만 걸린다(BuildModifiers(slot)).</summary>
    public sealed class TraitDef
    {
        public string Id, Title, Description;
        public string Recipe;                      // 그 캐릭터 전용(null = 누구나)
        public bool SkillSwap;                     // 필살기 교체(캐릭터마다 하나)
        public bool Rare;                          // ★ 희귀 특성(보라 테, Lv5+에서 50%로 등장)
        public string Icon;                        // TraitIcons id(null이면 아이콘 없음)
        public System.Action<PartyModifiers> Modify;
    }

    /// <summary>레벨이 올라 고를 특성(3개 중 1개). 정비 화면에서 고른다.</summary>
    public sealed class TraitPick
    {
        public int Slot, Level;
        public readonly List<string> Options = new();
    }

    /// <summary>
    /// 레벨업 특성(임시값): 레벨이 오를 때마다 그 파티원이 3개 중 1개를 영구로 얻는다(같은 특성은 한 번).
    /// 갈래(지속 피해·연쇄·탱킹·마법)를 유물과 맞춰 두어, 유물과 특성을 같은 쪽으로 몰수록 강해진다.
    /// 필살기 교체: 3레벨 첫 고르기엔 꼭 나오고(이후 확률), 고르면 그 캐릭터의 다른 필살기로 바뀐다(저지력 대신 다른 답).
    /// </summary>
    public static class TraitPool
    {
        public const int Choices = 3;
        public const int SwapLevel = 3;
        const double SwapChanceLater = 0.4;
        public const int RareLevel = 5;           // (임시값) 이 레벨부터 희귀 특성이 섞인다
        public const double RareChance = 0.5;     // (임시값) 희귀 특성이 한 칸 차지할 확률

        // Icon = TraitIcons.png 칸 id(hp, atk, aspd, cd, stop, wil, venom, hunt, leech, arcane, shield, bow, blade, swap, thorns, mana, scholar, berserk, rare)
        public static readonly TraitDef[] All =
        {
            T("t_hp", "강인함", "최대 체력 +12%", "hp", m => m.MaxHpMul *= 1.12f),
            T("t_dmg", "날카로움", "공격력 +10%", "atk", m => m.DamageMul *= 1.1f),
            T("t_aspd", "속사", "공격 속도 +12%", "aspd", m => m.AttackSpeedMul *= 1.12f),
            T("t_cd", "집중", "필살기 재사용 대기 -15%", "cd", m => m.SkillCooldownMul *= 0.85f),
            T("t_stop", "묵직한 일격", "필살기 저지력 +4", "stop", m => m.StopPowerAdd += 4),
            T("t_wil", "불굴", "정신력 +4 (방어력·저항)", "wil", m => m.StatBonus.Wil += 4),
            T("t_venom", "독 바르기", "공격에 독(3초 동안 초당 피해의 30%)", "venom", m => { m.PoisonRatio += 0.3f; m.PoisonDuration = System.MathF.Max(m.PoisonDuration, 3f); }),
            T("t_hunt", "사냥꾼", "적이 쓰러질 때마다 필살기 대기 -1초", "hunt", m => m.KillCooldownCut += 1f),
            T("t_leech", "흡혈", "흡혈 8%", "leech", m => m.SelfLifesteal += 0.08f),
            T("t_arcane", "비전 각인", "필살기가 마법 피해(보호막 관통), 지능 +3", "arcane", m => { m.MagicSkills = true; m.StatBonus.Int += 3; }),
            T("t_shield", "방패술", "방패 지속 +1.5초, 방패 동안 받는 피해 추가 -10%", "shield", m => { m.GuardDurationAdd += 1.5f; m.GuardMulCut += 0.1f; }, "Warrior"),
            T("t_power", "강궁", "필살기 피해 +25%", "bow", m => m.SkillDamageMul *= 1.25f, "Archer"),
            T("t_blade", "검무", "필살기 피해 +20%, 공격 속도 +6%", "blade", m => { m.SkillDamageMul *= 1.2f; m.AttackSpeedMul *= 1.06f; }, "Catherine"),
            // 필살기 교체: 끊기(저지력) 대신 다른 답
            Swap("s_catherine", "Catherine", "필살기 교체: 회오리 베기", "주변을 크게 벤다(광역 피해 큼, 저지력 없음)"),
            Swap("s_archer", "Archer", "필살기 교체: 마력 화살비", "앞쪽 넓은 땅에 마력 화살을 쏟는다(마법 피해, 저지력 없음)"),
            Swap("s_warrior", "Warrior", "필살기 교체: 철벽", "주변 파티원 전원에게 방패(받는 피해 -60%, 도발·저지력 없음)"),
            // 대가가 있는 특성(임시값): 갈래를 깊게 — 반사 · 마나 · 경험치 · 공격
            T("t_thorns", "가시 갑옷", $"근접으로 맞으면 받은 피해의 25%를 돌려줌  {Cost("이동 속도 -5%")}", "thorns", m => { m.ThornsAdd += 0.25f; m.MoveSpeedMul *= 0.95f; }),
            T("t_mana", "명상", $"마나 회복 +50%  {Cost("최대 체력 -5%")}", "mana", m => { m.ManaRegenMul *= 1.5f; m.MaxHpMul *= 0.95f; }),
            T("t_scholar", "학자", $"처치 경험치 +30%  {Cost("공격력 -5%")}", "scholar", m => { m.XpMul *= 1.3f; m.DamageMul *= 0.95f; }),
            T("t_siphon", "흡수", "적이 쓰러질 때마다 마나 +6", "mana", m => m.KillManaAdd += 6f),
            T("t_berserk", "광전사", $"공격력 +20%  {Cost("전투 후 회복 -8%p")}", "berserk", m => { m.DamageMul *= 1.2f; m.PostBattleHealAdd -= 0.08f; }),
            T("t_edge", "예리한 검기", $"필살기 저지력 +6  {Cost("필살기 재사용 대기 +15%")}", "stop", m => { m.StopPowerAdd += 6; m.SkillCooldownMul *= 1.15f; }, "Catherine"),
            T("t_iron", "강철 피부", $"최대 체력 +20%  {Cost("이동 속도 -10%")}", "hp", m => { m.MaxHpMul *= 1.2f; m.MoveSpeedMul *= 0.9f; }, "Warrior"),
            T("t_volley", "연사", $"공격 속도 +20%  {Cost("공격력 -8%")}", "aspd", m => { m.AttackSpeedMul *= 1.2f; m.DamageMul *= 0.92f; }, "Archer"),
            // ★ 희귀(임시값): Lv5 이상 고르기에서 50%로 한 칸
            R("t_r_echo", "잔향", $"적이 쓰러질 때마다 필살기 대기 -2초  {Cost("최대 체력 -10%")}", "hunt", m => { m.KillCooldownCut += 2f; m.MaxHpMul *= 0.9f; }),
            R("t_r_titan", "거인화", $"모든 스탯 +3  {Cost("공격 속도 -10%")}", "wil", m => { m.StatBonus.Str += 3; m.StatBonus.Agi += 3; m.StatBonus.Int += 3; m.StatBonus.Wil += 3; m.AttackSpeedMul *= 0.9f; }),
            R("t_r_venom", "맹독의 대가", $"공격에 독(4초 동안 초당 피해의 50%)  {Cost("공격력 -10%")}", "venom", m => { m.PoisonRatio += 0.5f; m.PoisonDuration = System.MathF.Max(m.PoisonDuration, 4f); m.DamageMul *= 0.9f; }),
        };

        /// <summary>설명 속 대가 부분(빨강 = 부정 신호 전용. 리치 텍스트).</summary>
        static string Cost(string text) => $"<color={RelicPool.CostHex}>{text}</color>";

        static TraitDef T(string id, string title, string desc, string icon, System.Action<PartyModifiers> modify, string recipe = null)
            => new() { Id = id, Title = title, Description = desc, Icon = icon, Modify = modify, Recipe = recipe };

        /// <summary>★ 희귀 특성(공용).</summary>
        static TraitDef R(string id, string title, string desc, string icon, System.Action<PartyModifiers> modify)
            => new() { Id = id, Title = title, Description = desc, Icon = icon, Modify = modify, Rare = true };

        static TraitDef Swap(string id, string recipe, string title, string desc)
            => new() { Id = id, Title = title, Description = desc, Recipe = recipe, Icon = "swap", SkillSwap = true, Modify = m => m.AltSpecial = true };

        public static TraitDef Find(string id)
        {
            foreach (var t in All) if (t.Id == id) return t;
            return null;
        }

        /// <summary>
        /// 그 파티원(recipe, 이미 가진 특성 owned)이 level이 되며 고를 특성 3개.
        /// 교체 칸(Lv3 보장·이후 확률) → ★ 희귀 칸(Lv5+에서 RareChance) → 나머지는 일반에서.
        /// </summary>
        public static TraitPick Roll(string recipe, IReadOnlyList<string> owned, int slot, int level, System.Random rng)
        {
            var pick = new TraitPick { Slot = slot, Level = level };
            var pool = new List<TraitDef>();
            var rares = new List<TraitDef>();
            TraitDef swap = null;
            foreach (var t in All)
            {
                if (Owns(owned, t.Id) || t.Recipe != null && t.Recipe != recipe) continue;
                if (t.SkillSwap) { swap = t; continue; }
                if (t.Rare) rares.Add(t); else pool.Add(t);
            }
            bool firstSwap = level == SwapLevel;
            if (swap != null && level >= SwapLevel && (firstSwap || rng.NextDouble() < SwapChanceLater)) pick.Options.Add(swap.Id);
            if (level >= RareLevel && rares.Count > 0 && rng.NextDouble() < RareChance) pick.Options.Add(rares[rng.Next(rares.Count)].Id);
            while (pick.Options.Count < Choices && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                pick.Options.Add(pool[i].Id);
                pool.RemoveAt(i);
            }
            return pick;
        }

        static bool Owns(IReadOnlyList<string> owned, string id)
        {
            if (owned == null) return false;
            for (int i = 0; i < owned.Count; i++) if (owned[i] == id) return true;
            return false;
        }
    }
}
