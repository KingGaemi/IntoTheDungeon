using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Runtime.Services
{
    public sealed class UnityInputService : IInputService
    {
        Vec2 _move; bool _atk;
        public void Set(Vec2 move, bool atkDown)
        {
            _move = move;
            _atk |= atkDown; // 래치
        }
        public Vec2 MoveAxis() => _move;
        public bool AttackPressed() => _atk;
        public void ConsumeFrame() { _atk = false; }
        public float GetAxis(string name) => UnityEngine.Input.GetAxisRaw(name);
        public bool GetButton(string name) => UnityEngine.Input.GetButton(name);
        public bool GetButtonDown(string name) => UnityEngine.Input.GetButtonDown(name);
        public bool GetButtonUp(string name) => UnityEngine.Input.GetButtonUp(name);
        public (float x, float y) GetMousePosition()
        {
            var p = UnityEngine.Input.mousePosition;
            return (p.x, p.y);
        }

    }
}