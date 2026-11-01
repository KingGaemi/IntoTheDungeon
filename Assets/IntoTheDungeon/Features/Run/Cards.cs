using System.Collections.Generic;

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
            CardDef.Passive("sharp", "날카로운 칼날", "파티 공격력 +20%", m => m.DamageMul *= 1.2f),
            CardDef.Passive("vigor", "강인한 체력", "파티 최대 체력 +25%", m => m.MaxHpMul *= 1.25f),
            CardDef.Passive("haste", "날랜 손놀림", "파티 공격 속도 +15%", m => m.AttackSpeedMul *= 1.15f),
            CardDef.Passive("stride", "가벼운 발걸음", "파티 이동 속도 +15%", m => m.MoveSpeedMul *= 1.15f),
            CardDef.Passive("focus", "집중", "필살기 재사용 대기 -20%", m => m.SkillCooldownMul *= 0.8f),
            CardDef.Passive("bulk", "묵직한 갑옷", "몸이 커지고 무거워짐(덜 밀림)", m => { m.BodyRadiusAdd += 0.05f; m.BodyMassMul *= 1.5f; }),
            CardDef.Passive("firstaid", "응급 처치", "전투 후 회복 +10%p", m => m.PostBattleHealAdd += 0.1f),
        };

        public static readonly CardDef[] Recruits =
        {
            CardDef.Recruit("recruit_catherine", "동료 영입: 케서린", "검사 동료가 파티에 합류", "Catherine"),
            CardDef.Recruit("recruit_archer", "동료 영입: 궁수", "뒤에서 화살을 쏘는 동료가 파티에 합류", "Archer"),
        };

        /// <summary>방 종류별 보상 3장: 일반은 패시브 3장, 정예는 동료 영입 1장 + 패시브 2장(파티가 가득이면 패시브 3장).</summary>
        public static List<CardDef> Offer(RoomType type, int partySize, System.Random rng)
        {
            var offer = new List<CardDef>(3);
            if (type == RoomType.Elite && partySize < MaxPartySize)
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
