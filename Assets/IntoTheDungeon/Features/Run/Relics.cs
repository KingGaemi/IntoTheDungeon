using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 파티 유물(임시값): 파티 전원에게 걸리는 영구 강화. 예전 패시브 카드를 옮겨 왔다(저장 호환을 위해 Id는 그대로).
    /// 파티 공용으로 개수 제한 없이 쌓이고(득실이 있는 유물은 고를 때 판단), 같은 유물은 두 번 얻지 않는다.
    /// 얻는 곳: 전투 방을 깰 때마다 3개 중 1개(정예는 한 단계, 보스는 스테이지 보상 티어), 상점 진열대(정비 1·상점 방 2, 등급별 가격).
    /// 등급별 등장 확률은 화면에 안 보이는 유물 티어(스테이지 진행·봉인석 난이도)로 정한다.
    /// 저주(Cursed): 강하지만 대가가 있다 — 일반 3택1·진열대 일반 칸엔 안 나오고 보스 보상 3번째 칸·상점 방 반값 칸·이벤트로만 온다.
    /// 이벤트 전용(EventOnly): 「?」방 사당에서만.
    /// 설명 규약: 대가 부분은 &lt;color=#FF6B61&gt;…&lt;/color&gt;(빨강 = 부정 신호 전용).
    /// </summary>
    public static class RelicPool
    {
        public const int RewardChoices = 3;
        public const int MaxTier = 4;

        public static readonly CardDef[] All =
        {
            // 일반
            CardDef.Relic("str", "거인의 팔찌", "파티 힘 +3 (공격력·체력·체력 회복)", Common, m => m.StatBonus.Str += 3),
            CardDef.Relic("agi", "바람의 깃털", "파티 민첩 +3 (공격 속도·이동 속도)", Common, m => m.StatBonus.Agi += 3),
            CardDef.Relic("int", "현자의 서", "파티 지능 +3 (마나·마나 회복·마법 공격력)", Common, m => m.StatBonus.Int += 3),
            CardDef.Relic("wil", "고요의 염주", "파티 정신력 +3 (방어력·마법 저항·넉백 저항·CC 저항)", Common, m => m.StatBonus.Wil += 3),
            CardDef.Relic("firstaid", "응급 붕대 묶음", "전투 후 회복 +10%p", Common, m => m.PostBattleHealAdd += 0.1f),
            CardDef.Relic("warcry", "전쟁의 뿔피리", "도발 범위 +50%", Common, m => m.TauntRadiusMul *= 1.5f),
            // 고급
            CardDef.Relic("focus", "집중의 모래시계", "필살기 재사용 대기 -20%", Uncommon, m => m.SkillCooldownMul *= 0.8f),
            CardDef.Relic("bulk", "무쇠 닻", "몸이 커지고 무거워짐(덜 밀림)", Uncommon, m => { m.BodyRadiusAdd += 0.05f; m.BodyMassMul *= 1.5f; }),
            CardDef.Relic("coin", "행운의 금화", "처치 골드 +25%", Uncommon, m => m.GoldMul *= 1.25f),
            CardDef.Relic("boots", "질주의 장화", "파티 이동 속도 +12%", Uncommon, m => m.MoveSpeedMul *= 1.12f),
            CardDef.Relic("axe", "처형인의 도끼", "기절한 적에게 주는 피해 +50%", Uncommon, m => m.GroggyDamageAdd += 0.5f),
            CardDef.Relic("quill", "관통 깃촉", "평타 화살이 적을 꿰뚫음", Uncommon, m => m.PierceArrows = true),
            CardDef.Relic("spikes", "가시 방패", "방패를 든 동안 받은 피해만큼 때린 적에게 돌려줌", Uncommon, m => m.GuardReflect += 1f),
            // 갈래: 지속 피해(독) · 연쇄(처치) · 탱킹(방패) · 마법(보호막 관통)
            CardDef.Relic("venom", "독사의 송곳니", "파티 공격에 독(3초 동안 초당 피해의 25%)", Uncommon, m => { m.PoisonRatio += 0.25f; m.PoisonDuration = System.MathF.Max(m.PoisonDuration, 3f); }),
            CardDef.Relic("hunt", "사냥의 열기", "적이 쓰러질 때마다 파티 필살기 대기 -0.6초", Uncommon, m => m.KillCooldownCut += 0.6f),
            CardDef.Relic("oath", "강철 맹세", "방패 지속 +2초, 방패 동안 받는 피해 추가 -15%", Uncommon, m => { m.GuardDurationAdd += 2f; m.GuardMulCut += 0.15f; }),
            // 갈래 확장(임시값): 독 지속 · 마나 · 반사 · 경제
            CardDef.Relic("nightshade", "벨라돈나", "파티 공격에 독 +10%, 독 지속 5초", Uncommon, m => { m.PoisonRatio += 0.1f; m.PoisonDuration = System.MathF.Max(m.PoisonDuration, 5f); }),
            CardDef.Relic("siphon", "영혼 흡입관", "적이 쓰러질 때마다 파티 마나 +8", Uncommon, m => m.KillManaAdd += 8f),
            CardDef.Relic("bramble", "가시 갑주", "근접으로 맞으면 받은 피해의 20%를 돌려줌", Uncommon, m => m.ThornsAdd += 0.2f),
            CardDef.Relic("wellspring", "마나 샘", "파티 마나 회복 +50%", Uncommon, m => m.ManaRegenMul *= 1.5f),
            CardDef.Relic("ledger", "상인의 장부", "상점 가격 -20%", Uncommon, m => m.ShopPriceMul *= 0.8f),
            // 희귀
            CardDef.Relic("crest", "영웅의 문장", "파티 모든 스탯 +2", Rare, m => { m.StatBonus.Str += 2; m.StatBonus.Agi += 2; m.StatBonus.Int += 2; m.StatBonus.Wil += 2; }),
            CardDef.Relic("fang", "분노의 송곳니", "파티 공격력 +12%", Rare, m => m.DamageMul *= 1.12f),
            CardDef.Relic("heart", "거인의 심장", "파티 최대 체력 +15%", Rare, m => m.MaxHpMul *= 1.15f),
            CardDef.Relic("anvil", "저지의 모루", "파티 필살기 저지력 +5", Rare, m => m.StopPowerAdd += 5),
            CardDef.Relic("echo", "메아리 종", "패턴을 끊으면 그 필살기가 바로 다시 준비되고 마나 절반을 돌려받음", Rare, m => m.BreakEcho = true),
            CardDef.Relic("ember", "불씨 주머니", "쓰러진 적이 터져 주변 적에게 그 최대 체력 35% 피해", Rare, m => m.CorpseBlast += 0.35f),
            CardDef.Relic("chalice", "피의 성배", "파티 흡혈 6%", Rare, m => m.Lifesteal += 0.06f),
            CardDef.Relic("rot", "부패의 손길", "중독된 적이 받는 피해 +30%", Rare, m => m.RotDamageAdd += 0.3f),
            CardDef.Relic("rune", "비전 룬", "필살기가 마법 피해(보호막 관통), 필살기 피해 +15%", Rare, m => { m.MagicSkills = true; m.SkillDamageMul *= 1.15f; }),
            CardDef.Relic("plague", "역병의 손가락", "중독된 적이 쓰러지면 남은 독 피해의 80%가 주변 적에게", Rare, m => m.PoisonBurst += 0.8f),
            CardDef.Relic("tome", "전술 교본", "처치 경험치 +30%", Rare, m => m.XpMul *= 1.3f),
            // 영웅
            CardDef.Relic("horn", "전쟁 군주의 뿔", "파티 공격력 +15%, 공격 속도 +10%", Epic, m => { m.DamageMul *= 1.15f; m.AttackSpeedMul *= 1.1f; }),
            CardDef.Relic("titan", "티탄의 쐐기", "파티 필살기 저지력 +10, 힘 +2", Epic, m => { m.StopPowerAdd += 10; m.StatBonus.Str += 2; }),
            CardDef.Relic("phoenix", "불사조 깃털", "전투 후 회복 +25%p, 최대 체력 +10%", Epic, m => { m.PostBattleHealAdd += 0.25f; m.MaxHpMul *= 1.1f; }),
            // 저주(임시값): 강하지만 대가가 있다. 일반 3택1엔 안 나온다(보스 보상 3번째 칸·상점 방 반값 칸·이벤트)
            CardDef.Relic("bloodpact", "피의 계약", $"파티 공격력 +30%  {Cost("전투 후 회복 -15%p")}", Epic,
                m => { m.DamageMul *= 1.3f; m.PostBattleHealAdd -= 0.15f; }, RelicFlags.Cursed),
            CardDef.Relic("greed", "탐욕의 금화", $"처치 골드 +60%  {Cost("처치 경험치 -25%")}", Rare,
                m => { m.GoldMul *= 1.6f; m.XpMul *= 0.75f; }, RelicFlags.Cursed),
            CardDef.Relic("leadboots", "납 장화", $"파티 최대 체력 +25%  {Cost("이동 속도 -15%, 필살기 대기 +10%")}", Rare,
                m => { m.MaxHpMul *= 1.25f; m.MoveSpeedMul *= 0.85f; m.SkillCooldownMul *= 1.1f; }, RelicFlags.Cursed),
            CardDef.Relic("glasscannon", "유리 대포", $"파티 필살기 피해 +50%  {Cost("최대 체력 -20%")}", Epic,
                m => { m.SkillDamageMul *= 1.5f; m.MaxHpMul *= 0.8f; }, RelicFlags.Cursed),
            CardDef.Relic("rustyanvil", "녹슨 모루", $"파티 필살기 저지력 +6  {Cost("공격 속도 -10%")}", Uncommon,
                m => { m.StopPowerAdd += 6; m.AttackSpeedMul *= 0.9f; }, RelicFlags.Cursed),
            // 이벤트 전용(임시값): 「?」방 사당
            CardDef.Relic("shrinemark", "사당의 표식", $"파티 필살기 저지력 +3  {Cost("최대 체력 -10%")}", Common,
                m => { m.StopPowerAdd += 3; m.MaxHpMul *= 0.9f; }, RelicFlags.EventOnly),
        };

        /// <summary>설명 속 대가 부분(빨강 = 부정 신호 전용. 리치 텍스트).</summary>
        public const string CostHex = "#FF6B61";
        static string Cost(string text) => $"<color={CostHex}>{text}</color>";

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
                if (!run.Relics.Contains(r) && (exclude == null || !exclude.Contains(r)) && r.Flags == RelicFlags.None) pool.Add(r);   // 저주·이벤트 전용은 일반 풀에 안 나옴

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

        /// <summary>안 가진 저주 유물 중 하나(없으면 null). 보스 보상 3번째 칸·이벤트(저주받은 상자 등)용.</summary>
        public static CardDef RollCursed(RunData run, System.Random rng)
        {
            var pool = new List<CardDef>();
            foreach (var r in All)
                if (r.Cursed && !run.Relics.Contains(r)) pool.Add(r);
            return pool.Count > 0 ? pool[rng.Next(pool.Count)] : null;
        }

        /// <summary>
        /// 일반 풀(Flags None)에서 등급 ≥ min인 안 가진 유물 하나(exclude 제외).
        /// 없으면 등급 조건을 내려 아무거나, 그래도 없으면 null.
        /// </summary>
        public static CardDef RollRarity(RunData run, ItemRarity min, System.Random rng, ICollection<CardDef> exclude = null)
        {
            var pool = new List<CardDef>();
            foreach (var r in All)
                if (r.Flags == RelicFlags.None && !run.Relics.Contains(r) && (exclude == null || !exclude.Contains(r))) pool.Add(r);
            if (pool.Count == 0) return null;
            var high = pool.FindAll(c => c.Rarity >= min);
            if (high.Count > 0) return high[rng.Next(high.Count)];
            return pool[rng.Next(pool.Count)];
        }
    }
}
