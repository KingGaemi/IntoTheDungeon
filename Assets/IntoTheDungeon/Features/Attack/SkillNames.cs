using IntoTheDungeon.Features.Character;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>필살기 프리셋의 화면 이름·한 줄 설명(툴팁·정비 카드·버튼 공용).</summary>
    public static class SkillNames
    {
        public static string Of(SkillPreset p) => p switch
        {
            SkillPreset.SwordWave => "검기",
            SkillPreset.PowerShot => "강화 화살",
            SkillPreset.TauntGuard => "도발 방패",
            SkillPreset.Whirlwind => "회오리 베기",
            SkillPreset.ArrowRain => "마력 화살비",
            SkillPreset.Bulwark => "철벽",
            _ => "필살기"
        };

        /// <summary>한 줄 설명(저지력 문구 포함 — StopPowerUi.Highlight가 청록으로 강조한다).</summary>
        public static string Describe(SkillPreset p) => p switch
        {
            SkillPreset.SwordWave => "앞으로 파고들며 큰 검기 한 방. 저지력 10",
            SkillPreset.PowerShot => "멀리 꿰뚫는 강화 화살 한 발. 저지력 10",
            SkillPreset.TauntGuard => "주변 적을 끌어모으고 방패(받는 피해 -50%). 저지력 10",
            SkillPreset.Whirlwind => "주변을 크게 벤다(광역 피해). 저지력 없음",
            SkillPreset.ArrowRain => "앞쪽 넓은 땅에 마력 화살(마법 피해). 저지력 없음",
            SkillPreset.Bulwark => "주변 파티원 전원에게 방패(받는 피해 -60%). 저지력 없음",
            _ => ""
        };
    }
}
