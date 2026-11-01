using System.Collections.Generic;

namespace IntoTheDungeon.Features.Character
{
    /// <summary>
    /// 레시피 이름 → 캐릭터 정의(이름·스탯 등). 설치기가 채워 월드 서비스로 등록한다.
    /// 캐릭터가 화면에 없을 때(정비 화면 등)도 파티원 정보를 보여 주려고 쓴다.
    /// </summary>
    public sealed class CharacterCatalog
    {
        readonly Dictionary<string, CharacterDefinition> _byName = new();

        public void Set(string recipe, CharacterDefinition def) => _byName[recipe] = def;

        public CharacterDefinition Get(string recipe)
            => recipe != null && _byName.TryGetValue(recipe, out var d) ? d : CharacterDefinition.Default;

        public string DisplayName(string recipe)
        {
            var d = Get(recipe);
            return string.IsNullOrEmpty(d.DisplayName) ? recipe : d.DisplayName;
        }
    }
}
