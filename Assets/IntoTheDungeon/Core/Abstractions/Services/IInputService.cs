using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface IInputService
    {
        void SetSnapshot(Vec2 move, bool attackHeld, bool attackDown, bool attackUp,
                        bool qDown, bool wDown, bool eDown, bool rDown);
        void SetChanged(bool axisChanged, bool heldChanged);
        void ConsumeFrame();

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

        // State Flags
        bool AxisChanged { get; }
        bool HeldChanged { get; }
        uint Seq { get; }
    }
}