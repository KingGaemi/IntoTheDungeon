using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Runtime.Services
{
    public sealed class UnityInputService : IInputService
    {
        // Movement & Attack
        public Vec2 Move { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool AttackDown { get; private set; }
        public bool AttackUp { get; private set; }

        // Skill Triggers
        public bool Q_Down { get; private set; }
        public bool W_Down { get; private set; }
        public bool E_Down { get; private set; }
        public bool R_Down { get; private set; }
        public bool JumpDown { get; private set; }
        public bool AutoToggleDown { get; private set; }
        public bool AllySpawnDown { get; private set; }
        public bool SkillDown => _skillPressed || Q_Down;
        bool _skillPressed;   // 버튼 클릭은 프레임 어디서든 올 수 있어 SetSnapshot이 덮어쓰지 않고 ConsumeFrame에서만 끈다

        // State Flags
        public bool AxisChanged { get; private set; }
        public bool HeldChanged { get; private set; }

        private uint _seq = 0;
        public uint Seq => _seq;

        public void SetSnapshot(Vec2 move, bool held, bool down, bool up,
                                bool qDown, bool wDown, bool eDown, bool rDown, bool jumpDown,
                                bool autoToggleDown, bool allySpawnDown)
        {
            Move = move;
            AttackHeld = held;
            AttackDown = down;
            AttackUp = up;

            Q_Down = qDown;
            W_Down = wDown;
            E_Down = eDown;
            R_Down = rDown;
            JumpDown = jumpDown;
            AutoToggleDown = autoToggleDown;
            AllySpawnDown = allySpawnDown;

            _seq++;
        }

        public void SetChanged(bool axisChanged, bool heldChanged)
        {
            AxisChanged = axisChanged;
            HeldChanged = heldChanged;
        }

        public void PressSkill()
        {
            _skillPressed = true;
            _seq++;
        }

        public void ConsumeFrame()
        {
            _skillPressed = false;
            // Edge 플래그 리셋
            AttackDown = false;
            AttackUp = false;
            Q_Down = false;
            W_Down = false;
            E_Down = false;
            R_Down = false;
            JumpDown = false;
            AutoToggleDown = false;
            AllySpawnDown = false;
            AxisChanged = false;
            HeldChanged = false;
        }
    }
}