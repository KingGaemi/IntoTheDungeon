using IntoTheDungeon.Core.Abstractions.Services;

namespace IntoTheDungeon.Core.Runtime.Services
{
    public sealed class UnityInputService : IInputService
    {
        // 키보드와 버튼은 프레임 안 어느 시점에든 들어올 수 있어 덮어쓰지 않고 모았다가 ConsumeFrame에서만 비운다
        int _skillSlot = -1;

        public int SkillSlot => _skillSlot;
        public bool R_Down { get; private set; }
        public bool AllySpawnDown { get; private set; }

        public void SetKeys(int skillSlot, bool rDown, bool allySpawnDown)
        {
            if (skillSlot >= 0) _skillSlot = skillSlot;
            R_Down |= rDown;
            AllySpawnDown |= allySpawnDown;
        }

        public void PressSkill(int slot)
        {
            if (slot >= 0) _skillSlot = slot;
        }

        public void ConsumeFrame()
        {
            _skillSlot = -1;
            R_Down = false;
            AllySpawnDown = false;
        }
    }
}
