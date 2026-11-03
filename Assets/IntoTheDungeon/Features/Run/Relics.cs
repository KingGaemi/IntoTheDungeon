using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 파티 유물(임시값): 파티 전원에게 걸리는 영구 강화. 예전 패시브 카드를 옮겨 왔다(저장 호환을 위해 Id는 그대로).
    /// 파티 공용으로 개수 제한 없이 쌓이고(득실이 있는 유물은 고를 때 판단), 같은 유물은 두 번 얻지 않는다.
    /// 얻는 곳: 스테이지 보스를 깨면 3개 중 1개(무조건), 상점(골드).
    /// 등급별 등장 확률은 화면에 안 보이는 유물 티어(스테이지 진행·봉인석 난이도)로 정한다.
    /// </summary>
    public static class RelicPool
    {
        public const int RewardChoices = 3;
        public const int MaxTier = 4;

        public static readonly CardDef[] All =
        {
            // 일반
            CardDef.Relic("str", "거인의 팔찌", "파티 힘 +3 (공격력·체력·체력 회복)", Common, m => m.StatBonus.Str += 3),
            CardDef.Relic("agi", "바람의 깃털", "파티 민첩 +3 (공격 속도·방어력·이동 속도)", Common, m => m.StatBonus.Agi += 3),
            CardDef.Relic("int", "현자의 서", "파티 지능 +3 (마나·마나 회복·마법 공격력)", Common, m => m.StatBonus.Int += 3),
            CardDef.Relic("wil", "고요의 염주", "파티 정신력 +3 (마법 저항·넉백 저항·CC 저항)", Common, m => m.StatBonus.Wil += 3),
            CardDef.Relic("firstaid", "응급 붕대 묶음", "전투 후 회복 +10%p", Common, m => m.PostBattleHealAdd += 0.1f),
            // 고급
            CardDef.Relic("focus", "집중의 모래시계", "필살기 재사용 대기 -20%", Uncommon, m => m.SkillCooldownMul *= 0.8f),
            CardDef.Relic("bulk", "무쇠 닻", "몸이 커지고 무거워짐(덜 밀림)", Uncommon, m => { m.BodyRadiusAdd += 0.05f; m.BodyMassMul *= 1.5f; }),
            CardDef.Relic("coin", "행운의 금화", "처치 골드 +25%", Uncommon, m => m.GoldMul *= 1.25f),
            CardDef.Relic("boots", "질주의 장화", "파티 이동 속도 +12%", Uncommon, m => m.MoveSpeedMul *= 1.12f),
            // 희귀
            CardDef.Relic("crest", "영웅의 문장", "파티 모든 스탯 +2", Rare, m => { m.StatBonus.Str += 2; m.StatBonus.Agi += 2; m.StatBonus.Int += 2; m.StatBonus.Wil += 2; }),
            CardDef.Relic("fang", "분노의 송곳니", "파티 공격력 +12%", Rare, m => m.DamageMul *= 1.12f),
            CardDef.Relic("heart", "거인의 심장", "파티 최대 체력 +15%", Rare, m => m.MaxHpMul *= 1.15f),
            CardDef.Relic("anvil", "저지의 모루", "파티 필살기 저지력 +5", Rare, m => m.StopPowerAdd += 5),
            // 영웅
            CardDef.Relic("horn", "전쟁 군주의 뿔", "파티 공격력 +15%, 공격 속도 +10%", Epic, m => { m.DamageMul *= 1.15f; m.AttackSpeedMul *= 1.1f; }),
            CardDef.Relic("titan", "티탄의 쐐기", "파티 필살기 저지력 +10, 힘 +2", Epic, m => { m.StopPowerAdd += 10; m.StatBonus.Str += 2; }),
            CardDef.Relic("phoenix", "불사조 깃털", "전투 후 회복 +25%p, 최대 체력 +10%", Epic, m => { m.PostBattleHealAdd += 0.25f; m.MaxHpMul *= 1.1f; }),
        };

        const ItemRarity Common = ItemRarity.Common, Uncommon = ItemRarity.Uncommon, Rare = ItemRarity.Rare, Epic = ItemRarity.Epic;

        public static CardDef Find(string id)
        {
            foreach (var r in All) if (r.Id == id) return r;
            return null;
        }

        /// <summary>
        /// 유물 티어(화면엔 안 보임, 0~4): 스테이지(2스테이지 +2) + 스테이지 보스 보상(+1) + 봉인석 3단계마다 +1.
        /// 1스테이지 상점은 0(일반·고급만), 1스테이지 보스 보상은 1(희귀 조금), 2스테이지부터 영웅이 나온다.
        /// </summary>
        public static int Tier(RunData run, bool stageReward)
            => System.Math.Clamp((run.Stage - 1) * 2 + (stageReward ? 1 : 0) + run.SealTier / 3, 0, MaxTier);

        // 티어별 등급 가중치: 일반 · 고급 · 희귀 · 영웅
        static readonly float[][] Weights =
        {
            new[] { 70f, 30f, 0f, 0f },
            new[] { 45f, 40f, 15f, 0f },
            new[] { 25f, 40f, 28f, 7f },
            new[] { 15f, 35f, 35f, 15f },
            new[] { 5f, 30f, 40f, 25f },
        };

        /// <summary>등급별 상점 가격.</summary>
        public static int Price(ItemRarity r) => r switch
        {
            ItemRarity.Uncommon => 90,
            ItemRarity.Rare => 150,
            ItemRarity.Epic => 240,
            _ => 50
        };

        /// <summary>
        /// 아직 없는 유물 중 서로 다른 count개. 칸마다 티어 가중치로 등급을 먼저 뽑고 그 등급에서 고른다
        /// (그 등급에 남은 게 없으면 바로 아래 등급부터, 그래도 없으면 남은 아무거나).
        /// </summary>
        public static List<CardDef> Roll(RunData run, int count, int tier, System.Random rng, ICollection<CardDef> exclude = null)
        {
            var pool = new List<CardDef>();
            foreach (var r in All)
                if (!run.Relics.Contains(r) && (exclude == null || !exclude.Contains(r))) pool.Add(r);

            var picked = new List<CardDef>(count);
            var w = Weights[System.Math.Clamp(tier, 0, MaxTier)];
            while (picked.Count < count && pool.Count > 0)
            {
                float total = w[0] + w[1] + w[2] + w[3];
                double roll = rng.NextDouble() * total;
                int rarity = 0;
                for (float acc = w[0]; rarity < 3 && roll >= acc; acc += w[++rarity]) { }

                CardDef pick = null;
                for (int r = rarity; r >= 0 && pick == null; r--)
                {
                    var same = pool.FindAll(c => (int)c.Rarity == r);
                    if (same.Count > 0) pick = same[rng.Next(same.Count)];
                }
                pick ??= pool[rng.Next(pool.Count)];
                picked.Add(pick);
                pool.Remove(pick);
            }
            return picked;
        }
    }
}
