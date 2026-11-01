using System.Collections.Generic;
using UnityEngine;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 아이템 Id → 아이콘 스프라이트. Resources/ItemIcons.asset 하나를 두고 화면 쪽(가방·바닥)에서 불러 쓴다.
    /// 새 아이템을 만들면 여기 줄을 하나 추가하면 된다(없으면 등급 색 상자만 보임).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemIcons", menuName = "IntoTheDungeon/ItemIconTable")]
    public sealed class ItemIconTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string itemId;
            public Sprite icon;
        }

        [SerializeField] Entry[] entries;

        Dictionary<string, Sprite> _map;
        static ItemIconTable _loaded;
        static bool _tried;

        public Sprite Get(string itemId)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, Sprite>();
                if (entries != null)
                    foreach (var e in entries)
                        if (!string.IsNullOrEmpty(e.itemId) && e.icon) _map[e.itemId] = e.icon;
            }
            return itemId != null && _map.TryGetValue(itemId, out var s) ? s : null;
        }

        /// <summary>Resources/ItemIcons에서 한 번 불러온다(없으면 null).</summary>
        public static Sprite Icon(string itemId)
        {
            if (!_tried) { _tried = true; _loaded = Resources.Load<ItemIconTable>("ItemIcons"); }
            return _loaded ? _loaded.Get(itemId) : null;
        }
    }
}
