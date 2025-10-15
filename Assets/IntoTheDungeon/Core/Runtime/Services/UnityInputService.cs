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

        // State Flags
        public bool AxisChanged { get; private set; }
        public bool HeldChanged { get; private set; }

        private uint _seq = 0;
        public uint Seq => _seq;

        public void SetSnapshot(Vec2 move, bool held, bool down, bool up,
                                bool qDown, bool wDown, bool eDown, bool rDown)
        {
            Move = move;
            AttackHeld = held;
            AttackDown = down;
            AttackUp = up;

            Q_Down = qDown;
            W_Down = wDown;
            E_Down = eDown;
            R_Down = rDown;

            _seq++;
        }

        public void SetChanged(bool axisChanged, bool heldChanged)
        {
            AxisChanged = axisChanged;
            HeldChanged = heldChanged;
        }

        public void ConsumeFrame()
        {
            // Edge 플래그 리셋
            AttackDown = false;
            AttackUp = false;
            Q_Down = false;
            W_Down = false;
            E_Down = false;
            R_Down = false;
            AxisChanged = false;
            HeldChanged = false;
        }
    }
}