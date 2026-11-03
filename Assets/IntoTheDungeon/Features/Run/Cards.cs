using System.Collections.Generic;
using IntoTheDungeon.Features.Stats;

namespace IntoTheDungeon.Features.Run
{
    public enum CardKind { Passive, Recruit }

    /// <summary>
    /// 덱에 쌓인 패시브 카드들의 합. 전투 시작 때 파티원 전원에게 입힌다(BattleFlowSystem).
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
        public readonly System.Action<PartyModifiers> Modify;   // 패시브만
        public readonly string RecruitRecipe;                    // 동료 영입만

        CardDef(string id, CardKind kind, string title, string desc, System.Action<PartyModifiers> modify, string recipe)
        { Id = id; Kind = kind; Title = title; Description = desc; Modify = modify; RecruitRecipe = recipe; }

        public static CardDef Passive(string id, string title, string desc, System.Action<PartyModifiers> modify)
            => new(id, CardKind.Passive, title, desc, modify, null);

        public static CardDef Recruit(string id, string title, string desc, string recipe)
            => new(id, CardKind.Recruit, title, desc, null, recipe);
    }

    /// <summary>
    /// 카드 목록(임시값). 효과 수치·종류는 게임 설계가 정해지면 바꾼다.
    /// 패시브는 파티 전원에게 걸리고 같은 카드를 여러 번 얻으면 겹친다.
    /// </summary>
    public static class CardPool
    {
        public const int MaxPartySize = 4;   // 필살기 칸 수와 같다

        public static readonly CardDef[] Passives =
        {
            CardDef.Passive("str", "근력 단련", "파티 힘 +3 (공격력·체력·체력 회복)", m => m.StatBonus.Str += 3),
            CardDef.Passive("agi", "몸놀림 단련", "파티 민첩 +3 (공격 속도·방어력·이동 속도)", m => m.StatBonus.Agi += 3),
            CardDef.Passive("int", "지식의 서", "파티 지능 +3 (마나·마나 회복·마법 공격력)", m => m.StatBonus.Int += 3),
            CardDef.Passive("wil", "정신 수양", "파티 정신력 +3 (마법 저항·넉백 저항·CC 저항)", m => m.StatBonus.Wil += 3),
            CardDef.Passive("focus", "집중", "필살기 재사용 대기 -20%", m => m.SkillCooldownMul *= 0.8f),
            CardDef.Passive("bulk", "묵직한 갑옷", "몸이 커지고 무거워짐(덜 밀림)", m => { m.BodyRadiusAdd += 0.05f; m.BodyMassMul *= 1.5f; }),
            CardDef.Passive("firstaid", "응급 처치", "전투 후 회복 +10%p", m => m.PostBattleHealAdd += 0.1f),
        };

        public static readonly CardDef[] Recruits =
        {
            CardDef.Recruit("recruit_catherine", "동료 영입: 케서린", "검사 동료가 파티에 합류", "Catherine"),
            CardDef.Recruit("recruit_archer", "동료 영입: 궁수", "뒤에서 화살을 쏘는 동료가 파티에 합류", "Archer"),
            CardDef.Recruit("recruit_warrior", "동료 영입: 중갑 전사", "적을 끌어모으고 방패로 버티는 탱커가 파티에 합류", "Warrior"),
        };

        /// <summary>Id로 카드 찾기(저장 불러오기용). 없으면 null.</summary>
        public static CardDef Find(string id)
        {
            foreach (var c in Passives) if (c.Id == id) return c;
            foreach (var c in Recruits) if (c.Id == id) return c;
            return null;
        }

        /// <summary>방 종류별 보상 3장: 일반은 패시브 3장, 정예는 동료 영입 1장 + 패시브 2장(파티가 가득이면 패시브 3장).</summary>
        public static List<CardDef> Offer(RoomType type, int partySize, System.Random rng)
        {
            var offer = new List<CardDef>(3);
            if (type is RoomType.Elite or RoomType.Boss && partySize < MaxPartySize)
                offer.Add(Recruits[rng.Next(Recruits.Length)]);

            var pool = new List<CardDef>(Passives);
            while (offer.Count < 3 && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                offer.Add(pool[i]);
                pool.RemoveAt(i);   // 한 번에 같은 카드가 두 장 나오지 않게
            }
            return offer;
        }
    }
}
