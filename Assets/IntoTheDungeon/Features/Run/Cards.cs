using System.Collections.Generic;
using IntoTheDungeon.Features.Stats;

namespace IntoTheDungeon.Features.Run
{
    public enum CardKind { Recruit, Relic, Gold, Item }   // 끝에만 추가(저장은 Id 문자열이라 안전). Gold: 금화 보상 카드, Item: 아이템 보상 카드(정예)

    /// <summary>유물 성격. Cursed: 저주(대가 있음, 일반 3택1에 안 나옴), EventOnly: 이벤트 전용(일반 풀에 안 나옴).</summary>
    [System.Flags] public enum RelicFlags : byte { None = 0, Cursed = 1, EventOnly = 2 }

    /// <summary>
    /// 파티 유물·잼·장비 효과의 합. 전투 시작 때 파티원 전원에게 입힌다(BattleFlowSystem).
    /// 배율은 곱해서, 더하기는 더해서 쌓인다. 몸 크기는 BodyComponent의 보정값으로만 들어간다.
    /// </summary>
    public sealed class PartyModifiers
    {
        public float DamageMul = 1f;
        public float MaxHpMul = 1f;
        public float AttackSpeedMul = 1f;
        public float MoveSpeedMul = 1f;
        public float SkillCooldownMul = 1f;
        public float BodyRadiusAdd = 0f;
        public float BodyMassMul = 1f;
        public float PostBattleHealAdd = 0f;   // 전투 후 회복(최대 체력 비율)에 더해짐
        public bool Cleave;                     // 근접 평타가 범위 공격(특수 장비)
        public float GoldMul = 1f;              // 처치 골드 배율(잼)
        public int StopPowerAdd;                // 필살기 저지력 보너스(망치·철퇴 등 장비)
        public PrimaryStats StatBonus;          // 파티 전원 기본 스탯에 더해짐(세부 능력치는 다시 계산)
        // 싸우는 방식을 바꾸는 유물(CombatPerks로 전투에 들어감)
        public float GroggyDamageAdd;
        public bool BreakEcho;
        public float CorpseBlast;
        public float GuardReflect;
        public float Lifesteal;
        public bool PierceArrows;
        public float TauntRadiusMul = 1f;
        // 빌드 갈래(유물·레벨업 특성). 지속 피해·연쇄·탱킹·마법
        public float PoisonRatio;               // 평타·스킬에 독: 초당 (맞힌 피해 × 이 값), PoisonDuration초
        public float PoisonDuration;
        public float RotDamageAdd;              // 중독된 적이 받는 피해 +
        public float KillCooldownCut;           // 처치하면 처치한 파티원 필살기 대기 -초
        public float GuardDurationAdd;          // 방패(도발·철벽) 지속 +초
        public float GuardMulCut;               // 방패 받는 피해 배율 -(0.5 → 0.35 등)
        public float SelfLifesteal;             // 그 파티원만 흡혈(특성)
        public bool MagicSkills;                // 필살기가 마법 피해(마법 보호막을 뚫음) + 마법 공격력이 더해짐
        public float SkillDamageMul = 1f;       // 필살기 피해 배율
        public bool AltSpecial;                 // 필살기 교체(레벨업 특성)
        // 확장(유물·특성·이벤트). 기본값이면 아무 효과 없음
        public float XpMul = 1f;                // 처치 경험치 배율(유물·특성)
        public float ShopPriceMul = 1f;         // 상점 가격 배율(상인의 장부)
        public float ManaRegenMul = 1f;         // 마나 회복 배율(마나 샘·명상)
        public float KillManaAdd;               // 적이 쓰러질 때 파티원 마나 +(영혼 흡입관·흡수)
        public float ThornsAdd;                 // 근접으로 맞으면 맞힌 피해 × 이 값을 돌려줌(가시 갑주·가시 갑옷)
        public float PoisonBurst;               // 중독된 적이 죽을 때 남은 독 피해 × 이 값을 주변 적에게(역병의 손가락)
    }

    public sealed class CardDef
    {
        public readonly string Id;
        public readonly CardKind Kind;
        public readonly string Title;
        public readonly string Description;
        public readonly System.Action<PartyModifiers> Modify;   // 유물만
        public readonly string RecruitRecipe;                    // 동료 영입만
        public readonly ItemRarity Rarity;                       // 유물만(등급: 색·등장 확률·가격)
        public readonly RelicFlags Flags;                        // 유물만(저주·이벤트 전용)
        public bool Cursed => (Flags & RelicFlags.Cursed) != 0;
        public bool EventOnly => (Flags & RelicFlags.EventOnly) != 0;
        public readonly int Amount;                              // Gold 카드: 금액
        public readonly int ItemIndex = -1;                      // Item 카드: ItemPool 번호

        CardDef(string id, CardKind kind, string title, string desc, System.Action<PartyModifiers> modify, string recipe, ItemRarity rarity = ItemRarity.Common,
                RelicFlags flags = RelicFlags.None, int amount = 0, int itemIndex = -1)
        { Id = id; Kind = kind; Title = title; Description = desc; Modify = modify; RecruitRecipe = recipe; Rarity = rarity; Flags = flags; Amount = amount; ItemIndex = itemIndex; }

        public static CardDef Relic(string id, string title, string desc, ItemRarity rarity, System.Action<PartyModifiers> modify, RelicFlags flags = RelicFlags.None)
            => new(id, CardKind.Relic, title, desc, modify, null, rarity, flags);

        public static CardDef Recruit(string id, string title, string desc, string recipe)
            => new(id, CardKind.Recruit, title, desc, null, recipe);

        /// <summary>금화 보상 카드(Id "gold:{금액}"). 고르면 그 금액을 바로 받는다.</summary>
        public static CardDef Gold(int amount)
            => new($"gold:{amount}", CardKind.Gold, "금화", "", null, null, ItemRarity.Common, RelicFlags.None, amount);

        /// <summary>아이템 보상 카드(Id "item:{번호}"). 고르면 주운 아이템 칸으로 들어간다.</summary>
        public static CardDef Item(ItemDef def)
            => new($"item:{def.Index}", CardKind.Item, def.Name, def.Description, null, null, def.Rarity, RelicFlags.None, 0, def.Index);
    }

    /// <summary>
    /// 동료 영입 카드 목록(임시값). 파티 강화 카드는 유물(RelicPool)로 옮겨 갔다.
    /// </summary>
    public static class CardPool
    {
        public const int MaxPartySize = 4;   // 필살기 칸 수와 같다
        public const string DefaultStarter = "Catherine";   // 처음부터 해금된 시작 캐릭터(타이틀에서 해금된 동료 중 하나를 골라 시작)
        // 동료는 지도의 동료 방에서 만난다(DungeonMap.PlaceAllies): 몹에 둘러싸여 버티고 있고, 같이 싸워 방을 깨면 합류
        public const string FirstRecruit = "Warrior";    // 1스테이지 첫 층: 중갑 전사(약한 케서린을 도발로 지켜 주는 그림을 먼저)
        public const string SecondRecruit = "Archer";    // 1스테이지 중반: 어느 길로 가든 만나는 첫 중간 전투 방

        public static readonly CardDef[] Recruits =
        {
            CardDef.Recruit("recruit_catherine", "동료 영입: 케서린", "검사 동료가 파티에 합류", "Catherine"),
            CardDef.Recruit("recruit_archer", "동료 영입: 궁수", "뒤에서 화살을 쏘는 동료가 파티에 합류", "Archer"),
            CardDef.Recruit("recruit_warrior", "동료 영입: 중갑 전사", "적을 끌어모으고 방패로 버티는 탱커가 파티에 합류", "Warrior"),
        };

        /// <summary>아직 파티에 없는 동료 영입 카드(같은 동료가 두 명이 되지 않게).</summary>
        public static List<CardDef> AvailableRecruits(IReadOnlyList<string> members)
        {
            var list = new List<CardDef>(Recruits.Length);
            foreach (var r in Recruits)
            {
                bool owned = false;
                for (int i = 0; i < members.Count; i++) if (members[i] == r.RecruitRecipe) { owned = true; break; }
                if (!owned) list.Add(r);
            }
            return list;
        }

        /// <summary>그 동료의 영입 카드(덱 기록용). 없으면 null.</summary>
        public static CardDef RecruitOf(string recipe)
        {
            foreach (var c in Recruits) if (c.RecruitRecipe == recipe) return c;
            return null;
        }

        /// <summary>Id로 카드 찾기(저장 불러오기용). 없으면 null.</summary>
        public static CardDef Find(string id)
        {
            if (id == null) return null;
            // 보상 카드 접두사: "gold:금액" / "item:번호"(저장에 Id로 남는다)
            if (id.StartsWith("gold:")) return int.TryParse(id.Substring(5), out int gold) ? CardDef.Gold(gold) : null;
            if (id.StartsWith("item:"))
            {
                if (!int.TryParse(id.Substring(5), out int idx)) return null;
                var def = ItemPool.Get(idx);
                return def != null ? CardDef.Item(def) : null;
            }
            var relic = RelicPool.Find(id);   // 예전 패시브 카드 Id = 유물 Id
            if (relic != null) return relic;
            foreach (var c in Recruits) if (c.Id == id) return c;
            return null;
        }
    }
}
