using IntoTheDungeon.Features.Core;
using UnityEditor.Playables;

namespace IntoTheDungeon.Features.Command
{
    public enum IntentKind { None, Move, Stop, Turn, Attack, Dodge, UseSkill }

    public readonly struct CharacterIntent
    {
        public readonly IntentKind Kind;
        // public readonly Vector2   Dir;       // Move용
        public readonly Facing2D Facing;
        public readonly int TargetId;   // Attack/Skill용 
        public readonly int SkillId;   // Skill용
        public readonly float Magnitude; // 속도/세기 등

        private CharacterIntent(IntentKind k, Facing2D facing, int targetId, int skillId, float mag)
        { Kind = k; Facing = facing; TargetId = targetId; SkillId = skillId; Magnitude = mag; }

        public static CharacterIntent Move()
            => new(IntentKind.Move, default, 0, 0, 0f);
        public static CharacterIntent Move(Facing2D facing)
            => new(IntentKind.Move, facing, 0, 0, 0f);

        public static CharacterIntent Stop()
            => new(IntentKind.Stop, default, 0, 0, 0f);
        public static CharacterIntent Attack()
            => new(IntentKind.Attack, default, 0, 0, 0f);
        public static CharacterIntent Attack(Facing2D facing)
            => new(IntentKind.Attack, facing, 0, 0, 0f);

        public static CharacterIntent Turn(Facing2D facing)
            => new(IntentKind.Turn, facing, 0, 0, 0f);


        public static CharacterIntent Skill(int skillId, int targetId = 0)
            => new(IntentKind.UseSkill, default, targetId, skillId, 0f);

        public static readonly CharacterIntent None = new(IntentKind.None, default, 0, 0, 0f);

        public bool Equals(CharacterIntent other)
        {
            return Kind == other.Kind
                && Facing == other.Facing
                && TargetId == other.TargetId
                && SkillId == other.SkillId
                && IntoTheDungeon.Core.Util.Maths.Mathx.Approximately(Magnitude , other.Magnitude,  0.0001f);
        }

    }
}