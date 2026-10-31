namespace IntoTheDungeon.Core.Abstractions.Services
{
    /// <summary>
    /// 플레이어 입력. 파티 전원이 자동전투하므로 직접 조작은 없고, 파티원별 필살기와 테스트용 소환만 받는다.
    /// 키보드(UnityInputDriver)와 화면 버튼(PressSkill) 모두 여기로 모이고, PlayerInputSystem이 한 번 읽은 뒤 ConsumeFrame으로 비운다.
    /// </summary>
    public interface IInputService
    {
        void SetKeys(int skillSlot, bool rDown, bool allySpawnDown);
        void PressSkill(int slot);   // 화면 필살기 버튼(0부터)
        void ConsumeFrame();

        int SkillSlot { get; }       // 이번 프레임에 누른 필살기 슬롯(0부터), 없으면 -1
        bool R_Down { get; }         // 오크 소환(테스트용)
        bool AllySpawnDown { get; }  // 동료 소환(테스트용)
    }
}
