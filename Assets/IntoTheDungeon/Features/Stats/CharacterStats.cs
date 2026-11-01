using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Features.Stats
{
    /// <summary>기본 스탯 4종. 캐릭터 정의(기본값)와 카드(보너스)가 이 값을 정하고, 세부 능력치는 StatFormula가 계산한다.</summary>
    [System.Serializable]
    public struct PrimaryStats
    {
        public int Str;   // 힘: 물리 공격력·최대 체력·체력 회복
        public int Agi;   // 민첩: 공격 속도·방어력·이동 속도
        public int Int;   // 지능: 최대 마나·마나 회복·마법 공격력
        public int Wil;   // 정신력: 마법 저항·넉백 저항·CC 저항

        public PrimaryStats(int str, int agi, int intel, int wil) { Str = str; Agi = agi; Int = intel; Wil = wil; }

        public static PrimaryStats operator +(PrimaryStats a, PrimaryStats b)
            => new(a.Str + b.Str, a.Agi + b.Agi, a.Int + b.Int, a.Wil + b.Wil);

        public static PrimaryStats operator *(PrimaryStats a, int k)
            => new(a.Str * k, a.Agi * k, a.Int * k, a.Wil * k);

        /// <summary>레벨 1의 기본값에 (레벨-1)만큼 성장치를 더한 값.</summary>
        public static PrimaryStats AtLevel(PrimaryStats baseStats, PrimaryStats growth, int level)
            => baseStats + growth * System.Math.Max(0, level - 1);

        public readonly bool IsZero => Str == 0 && Agi == 0 && Int == 0 && Wil == 0;

        public static readonly PrimaryStats Default = new(5, 5, 5, 5);
        public static readonly PrimaryStats DefaultGrowth = new(1, 1, 1, 1);
    }

    /// <summary>캐릭터의 기본 스탯: 정의값(레벨 1) + 레벨 성장치 × (레벨-1) + 카드·장비 보너스.</summary>
    public struct PrimaryStatsComponent : IComponentData
    {
        public PrimaryStats Base;     // 레벨 1 기준
        public PrimaryStats Growth;   // 레벨이 하나 오를 때마다 더해지는 값
        public int Level;
        public PrimaryStats Bonus;    // 카드(덱)·장비
        public readonly PrimaryStats Total => PrimaryStats.AtLevel(Base, Growth, Level) + Bonus;
    }

    /// <summary>기본 스탯에서 계산된 세부 능력치 중 StatusComponent에 없는 것들.</summary>
    public struct DerivedStatsComponent : IComponentData
    {
        public float Armor;             // 물리 피해 감소: 피해 × 100/(100+방어력)
        public float MagicResist;       // 마법 피해 감소(같은 식)
        public float KnockbackResist;   // 0~1: 밀리는 거리 비율 감소
        public float CcResist;          // 0~1: 경직 시간 감소
        public float HpRegen;           // 초당 체력 회복
        public float MaxMana;
        public float Mana;
        public float ManaRegen;         // 초당 마나 회복
        public float MagicDamage;       // 마법 공격력(마법 스킬이 생기면 사용)
        public float HpRegenCarry;      // 정수 체력에 못 미친 회복량 누적
    }

    /// <summary>
    /// 기본 스탯 → 세부 능력치 계산식(임시값). 수치 조정은 여기 상수만 바꾸면 된다.
    /// </summary>
    public static class StatFormula
    {
        // 힘
        public const float BaseHp = 150f, HpPerStr = 14f;   // 공격력 대비 체력을 넉넉히(전투가 몇 대 만에 끝나지 않게)
        public const float BaseDamage = 4f, DamagePerStr = 1.2f;
        public const float HpRegenPerStr = 0.15f;
        // 민첩
        public const float BaseAttackSpeed = 1f, AttackSpeedPerAgi = 0.03f;
        public const float ArmorPerAgi = 2f;
        public const float BaseMoveSpeed = 5f, MoveSpeedPerAgi = 0.015f;
        // 지능
        public const float BaseMana = 50f, ManaPerInt = 10f;
        public const float BaseManaRegen = 1f, ManaRegenPerInt = 0.3f;
        public const float MagicDamagePerInt = 1.5f;
        // 정신력
        public const float MagicResistPerWil = 2f;
        public const float KnockbackResistPerWil = 0.03f, MaxKnockbackResist = 0.6f;
        public const float CcResistPerWil = 0.03f, MaxCcResist = 0.8f;

        /// <summary>기본 스탯으로 StatusComponent(체력·공격력·공속·이속)와 세부 능력치를 새로 채운다. 현재 체력은 가득으로.</summary>
        public static void Apply(in PrimaryStats s, ref StatusComponent st, ref DerivedStatsComponent d)
        {
            st.MaxHp = (int)System.MathF.Round(BaseHp + HpPerStr * s.Str);
            st.CurrentHp = st.MaxHp;
            st.Damage = (int)System.MathF.Round(BaseDamage + DamagePerStr * s.Str);
            st.AttackSpeed = BaseAttackSpeed + AttackSpeedPerAgi * s.Agi;
            st.MovementSpeed = BaseMoveSpeed * (1f + MoveSpeedPerAgi * s.Agi);
            st.Armor = (int)System.MathF.Round(ArmorPerAgi * s.Agi);

            d.Armor = ArmorPerAgi * s.Agi;
            d.MagicResist = MagicResistPerWil * s.Wil;
            d.KnockbackResist = System.MathF.Min(MaxKnockbackResist, KnockbackResistPerWil * s.Wil);
            d.CcResist = System.MathF.Min(MaxCcResist, CcResistPerWil * s.Wil);
            d.HpRegen = HpRegenPerStr * s.Str;
            d.MaxMana = BaseMana + ManaPerInt * s.Int;
            d.Mana = d.MaxMana;
            d.ManaRegen = BaseManaRegen + ManaRegenPerInt * s.Int;
            d.MagicDamage = MagicDamagePerInt * s.Int;
            d.HpRegenCarry = 0f;
        }

        /// <summary>방어력·마법 저항으로 줄인 피해(최소 1).</summary>
        public static int Mitigate(int damage, float resist)
        {
            if (damage <= 0) return 0;
            float k = resist >= 0f ? 100f / (100f + resist) : 2f - 100f / (100f - resist);   // 음수 방어력이면 더 아픔
            return System.Math.Max(1, (int)System.MathF.Round(damage * k));
        }
    }
}
