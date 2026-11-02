using System.Collections.Generic;
using UnityEngine;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 캐릭터 레시피 → 초상화(대기 프레임에서 얼굴 쪽을 잘라 만든 그림). Resources/Portraits.asset 하나를 두고
    /// 정비 화면 파티원 카드·필살기 버튼에서 불러 쓴다. 없는 캐릭터는 초상화 없이 보인다.
    /// </summary>
    [CreateAssetMenu(fileName = "Portraits", menuName = "IntoTheDungeon/PortraitTable")]
    public sealed class PortraitTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string recipe;
            public Sprite portrait;
        }

        [SerializeField] Entry[] entries;

        Dictionary<string, Sprite> _map;
        static PortraitTable _loaded;
        static bool _tried;

        public Sprite Get(string recipe)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, Sprite>();
                if (entries != null)
                    foreach (var e in entries)
                        if (!string.IsNullOrEmpty(e.recipe) && e.portrait) _map[e.recipe] = e.portrait;
            }
            return recipe != null && _map.TryGetValue(recipe, out var s) ? s : null;
        }

        /// <summary>Resources/Portraits에서 한 번 불러온다(없으면 null).</summary>
        public static Sprite Of(string recipe)
        {
            if (!_tried) { _tried = true; _loaded = Resources.Load<PortraitTable>("Portraits"); }
            return _loaded ? _loaded.Get(recipe) : null;
        }
    }
}
