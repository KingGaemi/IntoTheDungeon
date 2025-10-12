using System;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Status
{
    public readonly struct HpModification
    {
        public enum ModKind : byte { Damage, Heal, Set /*, PercentBp*/ }
        public readonly ModKind Kind;
        public readonly int Value; // 항상 0 이상(규약)
        public readonly Entity Source; 

        private HpModification(ModKind k, int val, Entity src) { Kind = k; Value = val; Source = src; }

        public static HpModification Damage(int amount, Entity src)
        {
#if DEBUG
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
#endif
            return new(ModKind.Damage, amount, src);
        }
        public static HpModification Heal(int amount, Entity src)
        {
#if DEBUG
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
#endif
            return new(ModKind.Heal, amount, src);
        }
        public static HpModification Set(int hp, Entity src) => new(ModKind.Set, hp, src);
    }
}