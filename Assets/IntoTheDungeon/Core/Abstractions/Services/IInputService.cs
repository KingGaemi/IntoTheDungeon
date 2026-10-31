using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface IInputService
    {
        void SetSnapshot(Vec2 move, bool attackHeld, bool attackDown, bool attackUp,
                        bool qDown, bool wDown, bool eDown, bool rDown, bool jumpDown,
                        bool autoToggleDown, bool allySpawnDown);
        void SetChanged(bool axisChanged, bool heldChanged);
        void ConsumeFrame();
        void PressSkill();   // 화면 필살기 버튼: 다음 입력 처리 때 SkillDown으로 들어간다

        // Movement & Attack
        Vec2 Move { get; }
        bool AttackHeld { get; }
        bool AttackDown { get; }
        bool AttackUp { get; }

        // Skill Triggers
        bool Q_Down { get; }
        bool W_Down { get; }
        bool E_Down { get; }
        bool R_Down { get; }
        bool JumpDown { get; }
        bool AutoToggleDown { get; }   // 자동전투 켜기/끄기
        bool AllySpawnDown { get; }    // 동료 소환(테스트용)
        bool SkillDown { get; }        // 필살기(화면 버튼 또는 Q키)

        // State Flags
        bool AxisChanged { get; }
        bool HeldChanged { get; }
        uint Seq { get; }
    }
}