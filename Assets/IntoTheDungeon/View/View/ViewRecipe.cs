using UnityEngine;
using System;
using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Unity.View
{
    /// <summary>
    /// ViewRecipe 식별자 (int 래퍼)
    /// </summary>
    [Serializable]
    public struct RecipeId : IEquatable<RecipeId>
    {
        public int Value;

        public RecipeId(int value) => Value = value;

        public bool Equals(RecipeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RecipeId id && Equals(id);
        public override int GetHashCode() => Value;

        public static implicit operator int(RecipeId id) => id.Value;
        public static implicit operator RecipeId(int value) => new RecipeId(value);

        public override string ToString() => $"RecipeId({Value})";
    }

    /// <summary>
    /// Behaviour 타입 식별자 (문자열 대신 int 사용)
    /// </summary>
    [Serializable]
    public struct BehaviourTypeId : IEquatable<BehaviourTypeId>
    {
        public int Value;

        public BehaviourTypeId(int value) => Value = value;

        public bool Equals(BehaviourTypeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BehaviourTypeId id && Equals(id);
        public override int GetHashCode() => Value;

        public static implicit operator int(BehaviourTypeId id) => id.Value;
        public static implicit operator BehaviourTypeId(int value) => new BehaviourTypeId(value);
    }

    /// <summary>
    /// Behaviour 사양 (인스펙터용)
    /// </summary>
    [Serializable]
    public struct BehaviourRecipe
    {
        public BehaviourTypeId TypeId;
        [TextArea(2, 4)]
        public string PayloadJson; // 선택 사항: JSON 직렬화된 파라미터

        public BehaviourSpec ToBehaviourSpec(BehaviourTypeMap typeMap)
        {
            string typeName = typeMap.GetTypeName(TypeId);
            byte[] payload = string.IsNullOrEmpty(PayloadJson)
                ? null
                : System.Text.Encoding.UTF8.GetBytes(PayloadJson);

            return new BehaviourSpec
            {
                TypeId = TypeId,
                Payload = payload
            };
        }
    }

    /// <summary>
    /// 뷰 생성 레시피 (ScriptableObject 자산)
    /// </summary>
    [CreateAssetMenu(fileName = "NewViewRecipe", menuName = "IntoTheDungeon/View/Recipe")]
    public sealed class ViewRecipe : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] RecipeId _recipeId;

        [Header("Visual")]
        [SerializeField] GameObject _prefab;
        [SerializeField] short _sortingLayerId;
        [SerializeField] short _orderInLayer;

        [Header("Behaviours")]
        [SerializeField] BehaviourRecipe[] _behaviours = Array.Empty<BehaviourRecipe>();

        public RecipeId RecipeId => _recipeId;
        public GameObject Prefab => _prefab;

        /// <summary>
        /// ViewSpawnSpec 생성
        /// </summary>
        public SpawnData CreateSpawnData(ViewRecipeRegistry registry)
        {
            int prefabId = registry.GetPrefabId(_prefab);

            BehaviourSpec[] behaviourSpecs = null;
            if (_behaviours.Length > 0)
            {
                var typeMap = registry.BehaviourTypeMap;
                behaviourSpecs = new BehaviourSpec[_behaviours.Length];

                for (int i = 0; i < _behaviours.Length; i++)
                {
                    behaviourSpecs[i] = _behaviours[i].ToBehaviourSpec(typeMap);
                }
            }

            return new SpawnData
            {
                PrefabId = prefabId,
                SortingLayerId = _sortingLayerId,
                OrderInLayer = _orderInLayer,
                Behaviours = behaviourSpecs
            };
        }

        void OnValidate()
        {
            // 레시피 ID 자동 생성 (에디터에서만)
            if (_recipeId.Value == 0)
            {
                _recipeId = new RecipeId(GetInstanceID());
            }
        }
    }

    /// <summary>
    /// Behaviour TypeId → TypeName 매핑 테이블
    /// </summary>
    [CreateAssetMenu(fileName = "BehaviourTypeMap", menuName = "IntoTheDungeon/View/BehaviourTypeMap")]
    public sealed class BehaviourTypeMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public BehaviourTypeId TypeId;
            public string TypeName; // "IntoTheDungeon.View.ProjectileView"
        }

        [SerializeField] Entry[] _entries = Array.Empty<Entry>();

        readonly Dictionary<int, string> _cache = new();

        void OnEnable()
        {
            RebuildCache();
        }

        void RebuildCache()
        {
            _cache.Clear();
            foreach (var entry in _entries)
            {
                _cache[entry.TypeId.Value] = entry.TypeName;
            }
        }

        public string GetTypeName(BehaviourTypeId typeId)
        {
            if (_cache.Count == 0) RebuildCache();

            if (_cache.TryGetValue(typeId.Value, out string typeName))
                return typeName;

#if UNITY_EDITOR
            Debug.LogError($"[BehaviourTypeMap] TypeId {typeId.Value} not found");
#endif
            return string.Empty;
        }

        public bool TryGetTypeName(BehaviourTypeId typeId, out string typeName)
        {
            if (_cache.Count == 0) RebuildCache();
            return _cache.TryGetValue(typeId.Value, out typeName);
        }

#if UNITY_EDITOR
        public void RegisterType(BehaviourTypeId typeId, string typeName)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].TypeId.Equals(typeId))
                {
                    _entries[i].TypeName = typeName;
                    RebuildCache();
                    UnityEditor.EditorUtility.SetDirty(this);
                    return;
                }
            }

            // 새 엔트리 추가
            Array.Resize(ref _entries, _entries.Length + 1);
            _entries[_entries.Length - 1] = new Entry
            {
                TypeId = typeId,
                TypeName = typeName
            };
            RebuildCache();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// ViewRecipe 중앙 레지스트리
    /// </summary>
    [CreateAssetMenu(fileName = "ViewRecipeRegistry", menuName = "IntoTheDungeon/View/Registry")]
    public sealed class ViewRecipeRegistry : ScriptableObject
    {
        [SerializeField] ViewRecipe[] _recipes = Array.Empty<ViewRecipe>();
        [SerializeField] GameObject[] _prefabCatalog = Array.Empty<GameObject>();
        [SerializeField] BehaviourTypeMap _behaviourTypeMap;

        readonly Dictionary<int, ViewRecipe> _recipeCache = new();
        readonly Dictionary<GameObject, int> _prefabToIdCache = new();

        public BehaviourTypeMap BehaviourTypeMap => _behaviourTypeMap;
        public GameObject[] PrefabCatalog => _prefabCatalog;

        void OnEnable()
        {
            RebuildCaches();
        }

        void RebuildCaches()
        {
            _recipeCache.Clear();
            foreach (var recipe in _recipes)
            {
                if (recipe != null)
                    _recipeCache[recipe.RecipeId.Value] = recipe;
            }

            _prefabToIdCache.Clear();
            for (int i = 0; i < _prefabCatalog.Length; i++)
            {
                if (_prefabCatalog[i] != null)
                    _prefabToIdCache[_prefabCatalog[i]] = i;
            }
        }

        /// <summary>
        /// RecipeId로 ViewSpawnSpec 생성
        /// </summary>
        public SpawnData CreateSpawnData(RecipeId recipeId)
        {
            if (_recipeCache.Count == 0) RebuildCaches();

            if (_recipeCache.TryGetValue(recipeId.Value, out var recipe))
            {
                return recipe.CreateSpawnData(this);
            }

#if UNITY_EDITOR
            Debug.LogError($"[ViewRecipeRegistry] Recipe {recipeId} not found");
#endif

            return default;
        }

        /// <summary>
        /// 프리팹으로 PrefabId 조회
        /// </summary>
        public int GetPrefabId(GameObject prefab)
        {
            if (_prefabToIdCache.Count == 0) RebuildCaches();

            if (_prefabToIdCache.TryGetValue(prefab, out int id))
                return id;

#if UNITY_EDITOR
            Debug.LogError($"[ViewRecipeRegistry] Prefab {prefab.name} not in catalog");
#endif

            return -1;
        }

        /// <summary>
        /// RecipeId로 레시피 조회
        /// </summary>
        public bool TryGetRecipe(RecipeId recipeId, out ViewRecipe recipe)
        {
            if (_recipeCache.Count == 0) RebuildCaches();
            return _recipeCache.TryGetValue(recipeId.Value, out recipe);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 전용: 레시피 등록
        /// </summary>
        public void RegisterRecipe(ViewRecipe recipe)
        {
            if (recipe == null) return;

            for (int i = 0; i < _recipes.Length; i++)
            {
                if (_recipes[i] == recipe) return; // 이미 등록됨
            }

            Array.Resize(ref _recipes, _recipes.Length + 1);
            _recipes[_recipes.Length - 1] = recipe;
            RebuildCaches();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// 에디터 전용: 프리팹 카탈로그에 추가
        /// </summary>
        public int RegisterPrefab(GameObject prefab)
        {
            if (prefab == null) return -1;

            // 이미 등록된 경우
            for (int i = 0; i < _prefabCatalog.Length; i++)
            {
                if (_prefabCatalog[i] == prefab) return i;
            }

            // 빈 슬롯 찾기
            for (int i = 0; i < _prefabCatalog.Length; i++)
            {
                if (_prefabCatalog[i] == null)
                {
                    _prefabCatalog[i] = prefab;
                    RebuildCaches();
                    UnityEditor.EditorUtility.SetDirty(this);
                    return i;
                }
            }

            // 새 슬롯 추가
            int newId = _prefabCatalog.Length;
            Array.Resize(ref _prefabCatalog, newId + 1);
            _prefabCatalog[newId] = prefab;
            RebuildCaches();
            UnityEditor.EditorUtility.SetDirty(this);
            return newId;
        }

        /// <summary>
        /// 에디터 전용: 카탈로그 자동 정리
        /// </summary>
        [ContextMenu("Rebuild Catalog from Recipes")]
        public void RebuildCatalogFromRecipes()
        {
            var prefabs = new HashSet<GameObject>();
            foreach (var recipe in _recipes)
            {
                if (recipe != null && recipe.Prefab != null)
                    prefabs.Add(recipe.Prefab);
            }

            _prefabCatalog = new GameObject[prefabs.Count];
            int idx = 0;
            foreach (var prefab in prefabs)
                _prefabCatalog[idx++] = prefab;

            RebuildCaches();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ViewRecipeRegistry] Rebuilt catalog: {_prefabCatalog.Length} prefabs");
        }

        [ContextMenu("Validate All Recipes")]
        public void ValidateAllRecipes()
        {
            int errors = 0;
            foreach (var recipe in _recipes)
            {
                if (recipe == null) continue;

                if (recipe.Prefab == null)
                {
                    Debug.LogError($"Recipe {recipe.name} has null prefab", recipe);
                    errors++;
                }

                if (!_prefabToIdCache.ContainsKey(recipe.Prefab))
                {
                    Debug.LogError($"Recipe {recipe.name} prefab not in catalog", recipe);
                    errors++;
                }
            }

            if (errors == 0)
                Debug.Log("[ViewRecipeRegistry] All recipes valid");
            else
                Debug.LogError($"[ViewRecipeRegistry] Found {errors} errors");
        }
#endif
    }

    /// <summary>
    /// 런타임 헬퍼: RecipeId로 빠르게 스폰
    /// </summary>
    public static class ViewRecipeExtensions
    {
        public static void EnqueueSpawnFromRecipe(
            this IViewOpQueue queue,
            Entity entity,
            RecipeId recipeId,
            ViewRecipeRegistry registry)
        {
            var data = registry.CreateSpawnData(recipeId);
            queue.Enqueue(entity, in data);
        }
    }
}