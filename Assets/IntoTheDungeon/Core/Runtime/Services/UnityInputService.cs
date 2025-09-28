using IntoTheDungeon.Core.Abstractions.Services;

namespace IntoTheDungeon.Core.Runtime.Services
{
    public sealed class UnityInputService : IInputService
    {
        public float GetAxis(string name) => UnityEngine.Input.GetAxisRaw(name);
        public bool  GetButton(string name) => UnityEngine.Input.GetButton(name);
        public bool  GetButtonDown(string name) => UnityEngine.Input.GetButtonDown(name);
        public bool  GetButtonUp(string name) => UnityEngine.Input.GetButtonUp(name);
        public (float x, float y) GetMousePosition()
        {
            var p = UnityEngine.Input.mousePosition;
            return (p.x, p.y);
        }
    }
}