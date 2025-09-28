using UnityEngine;
using MyGame.GamePlay.Entity.Abstractions;
namespace MyGame.GamePlay.Entity.Characters.Abstractions
{
    public enum IntentKind { None, Move, Stop, Turn, Attack, Dodge, UseSkill }

    public readonly struct CharacterIntent
    {
        public readonly IntentKind Kind;
        // public readonly Vector2   Dir;       // Move용
        public readonly Facing2D? Facing;
        public readonly GameObject Target;   // Attack/Skill용 
        public readonly int SkillId;   // Skill용
        public readonly float Magnitude; // 속도/세기 등

        private CharacterIntent(IntentKind k, Facing2D? facing, GameObject target, int skillId, float mag)
        { Kind = k; Facing = facing; Target = target; SkillId = skillId; Magnitude = mag; }

        public static CharacterIntent Move()
            => new(IntentKind.Move, default, null, 0, 0f);
        public static CharacterIntent Move(Facing2D facing)
            => new(IntentKind.Move, facing, null, 0, 0f);

        public static CharacterIntent Stop()
            => new(IntentKind.Stop, default, null, 0, 0f);


        public static CharacterIntent Attack(Facing2D? facing = null)
            => new(IntentKind.Attack, facing, null, 0, 0f);

        public static CharacterIntent Turn(Facing2D facing)
            => new(IntentKind.Turn, facing, null, 0, 0f);


        public static CharacterIntent Skill(int skillId, GameObject target = null)
            => new(IntentKind.UseSkill, default, target, skillId, 0f);

        public static readonly CharacterIntent None = new(IntentKind.None, default, null, 0, 0f);
    }
}