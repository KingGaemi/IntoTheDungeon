using System.Collections.Generic;
using IntoTheDungeon.Features.Stats;

namespace IntoTheDungeon.Features.Run
{
    public enum CardKind { Recruit, Relic }

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

        CardDef(string id, CardKind kind, string title, string desc, System.Action<PartyModifiers> modify, string recipe, ItemRarity rarity = ItemRarity.Common)
        { Id = id; Kind = kind; Title = title; Description = desc; Modify = modify; RecruitRecipe = recipe; Rarity = rarity; }

        public static CardDef Relic(string id, string title, string desc, ItemRarity rarity, System.Action<PartyModifiers> modify)
            => new(id, CardKind.Relic, title, desc, modify, null, rarity);

        public static CardDef Recruit(string id, string title, string desc, string recipe)
            => new(id, CardKind.Recruit, title, desc, null, recipe);
    }

    /// <summary>
    /// 동료 영입 카드 목록(임시값). 파티 강화 카드는 유물(RelicPool)로 옮겨 갔다.
    /// </summary>
    public static class CardPool
    {
        public const int MaxPartySize = 4;   // 필살기 칸 수와 같다

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

        /// <summary>Id로 카드 찾기(저장 불러오기용). 없으면 null.</summary>
        public static CardDef Find(string id)
        {
            var relic = RelicPool.Find(id);   // 예전 패시브 카드 Id = 유물 Id
            if (relic != null) return relic;
            foreach (var c in Recruits) if (c.Id == id) return c;
            return null;
        }
    }
}
