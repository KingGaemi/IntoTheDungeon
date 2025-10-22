// Unity 쪽에 두기 권장: IntoTheDungeon.Unity.Catalogs
using System.Collections.Generic;
using UnityEngine;

namespace IntoTheDungeon.Unity.Catalogs
{
    [CreateAssetMenu(fileName = "SpriteCatalog", menuName = "IntoTheDungeon/Catalogs/SpriteCatalog")]
    public sealed class SpriteCatalogAsset : ScriptableObject, ISpriteCatalog
    {
        [System.Serializable]
        public struct Entry
        {
            public Sprite sprite;
            public int id; // 에디터에서 고정 발급
        }

        public Entry[] entries;

        Dictionary<Sprite, int> _map;

        void OnEnable()
        {
            _map = new Dictionary<Sprite, int>(entries?.Length ?? 0);
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].sprite) _map[entries[i].sprite] = entries[i].id;
        }

        public bool TryGetId(Sprite sprite, out int id)
        {
            id = -1;
            return sprite != null && _map != null && _map.TryGetValue(sprite, out id);
        }
    }
}
