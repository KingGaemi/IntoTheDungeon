namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface IInputService
    {
        float GetAxis(string name);                  // "Horizontal", "Vertical" 등
        bool  GetButton(string name);                // "Fire1"
        bool  GetButtonDown(string name);
        bool  GetButtonUp(string name);
        (float x, float y) GetMousePosition();       // 스크린 좌표
    }
}