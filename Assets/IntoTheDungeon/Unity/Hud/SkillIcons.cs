using UnityEngine;
using IntoTheDungeon.Features.Character;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 필살기 아이콘(Resources/SkillIcons.png, 코드로 그린 16px 도트 한 줄 시트). 칸 번호 = SkillPreset 열거 값:
    /// 0 SwordWave 검기 · 1 PowerShot 강화 화살 · 2 TauntGuard 도발 방패 · 3 Whirlwind 회오리 베기 · 4 ArrowRain 마력 화살비 · 5 Bulwark 철벽.
    /// </summary>
    public static class SkillIcons
    {
        const int Count = 6;
        static readonly Sprite[] s_cache = new Sprite[Count];
        static Texture2D s_sheet;

        public static Sprite Of(SkillPreset preset)
        {
            int i = (int)preset;
            if (i < 0 || i >= Count) return null;
            if (s_cache[i]) return s_cache[i];
            if (!s_sheet)
            {
                s_sheet = Resources.Load<Texture2D>("SkillIcons");
                if (!s_sheet) return null;
                s_sheet.filterMode = FilterMode.Point;
            }
            if ((i + 1) * 16 > s_sheet.width) return null;
            var sp = Sprite.Create(s_sheet, new Rect(i * 16, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            s_cache[i] = sp;
            return sp;
        }
    }
}
