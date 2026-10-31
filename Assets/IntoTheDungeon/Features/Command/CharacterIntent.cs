using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Util;


namespace IntoTheDungeon.Features.Command
{
    public enum IntentKind { None, Move, Stop, Turn, Attack, Dodge, UseSkill, Jump }

    public readonly struct CharacterIntent
    {
        public readonly IntentKind Kind;
        public readonly Vec2 Dir;           // Move용: 바닥 평면 방향(X 가로, Y 깊이). 정규화 안 해도 됨
        public readonly Facing2D Facing;
        public readonly int TargetId;   // Attack/Skill용
        public readonly int SkillId;   // Skill용
        public readonly float Magnitude; // 속도/세기 등

        private CharacterIntent(IntentKind k, Vec2 dir, Facing2D facing, int targetId, int skillId, float mag)
        { Kind = k; Dir = dir; Facing = facing; TargetId = targetId; SkillId = skillId; Magnitude = mag; }

        /// <summary>바닥 평면 방향으로 이동. 가로 성분이 있으면 그쪽을 바라본다.</summary>
        public static CharacterIntent Move(Vec2 dir)
            => new(IntentKind.Move, dir, dir.X < 0 ? Facing2D.Left : Facing2D.Right, 0, 0, 0f);
        /// <summary>가로로만 이동(기존 호환).</summary>
        public static CharacterIntent Move(Facing2D facing)
            => Move(new Vec2(facing.ToSign(), 0f));

        public static CharacterIntent Stop()
            => new(IntentKind.Stop, default, default, 0, 0, 0f);
        public static CharacterIntent Attack()
            => new(IntentKind.Attack, default, default, 0, 0, 0f);
        public static CharacterIntent Attack(Facing2D facing)
            => new(IntentKind.Attack, default, facing, 0, 0, 0f);

        public static CharacterIntent Jump()
            => new(IntentKind.Jump, default, default, 0, 0, 0f);

        public static CharacterIntent Turn(Facing2D facing)
            => new(IntentKind.Turn, default, facing, 0, 0, 0f);


        public static CharacterIntent Skill(int skillId, int targetId = 0)
            => new(IntentKind.UseSkill, default, default, targetId, skillId, 0f);

        public static readonly CharacterIntent None = new(IntentKind.None, default, default, 0, 0, 0f);

        public bool Equals(CharacterIntent other)
        {
            return Kind == other.Kind
                && Dir.ApproximatelyEquals(other.Dir)
                && Facing == other.Facing
                && TargetId == other.TargetId
                && SkillId == other.SkillId
                && IntoTheDungeon.Core.Util.Mathx.Approximately(Magnitude , other.Magnitude,  0.0001f);
        }

    }
}
