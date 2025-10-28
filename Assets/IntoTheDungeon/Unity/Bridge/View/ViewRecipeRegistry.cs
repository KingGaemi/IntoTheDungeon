using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Unity.Bridge.View.Abstractions;
using UnityEngine;

namespace IntoTheDungeon.Unity.Bridge.View
{
    [CreateAssetMenu(fileName = "ViewRecipeRegistry", menuName = "IntoTheDungeon/View/RecipeDatabase")]
    public sealed class ViewRecipeRegistry : ScriptableObject, IViewRecipeRegistry
    {
        [Header("Recipes")]
        [SerializeField] private ViewRecipe[] recipes;

        private Dictionary<ViewId, IViewRecipe> _recipeMap;

        public ViewRecipe[] Recipes => recipes;

        // 런타임 초기화
        public void Initialize()
        {
            _recipeMap = new Dictionary<ViewId, IViewRecipe>(recipes.Length);

            foreach (var recipe in recipes)
            {
                if (recipe == null)
                {
                    Debug.LogWarning($"[ViewRecipeRegistry] Null recipe, skipping");
                    continue;
                }

                var viewId = recipe.ViewId;

                if (_recipeMap.ContainsKey(viewId))
                {
                    Debug.LogWarning($"[ViewRecipeRegistry] Duplicate ViewId: {viewId} " +
                                   $"(collision between recipes with same behaviour structure)");
                    continue;
                }

                _recipeMap[viewId] = recipe;
            }

            Debug.Log($"[ViewRecipeRegistry] Loaded {_recipeMap.Count} recipes");
        }

        public bool TryGetRecipe(ViewId viewId, out IViewRecipe recipe)
        {
            if (_recipeMap == null)
            {
                Debug.LogError("[ViewRecipeRegistry] Not initialized! Call Initialize() first.");
                recipe = null;
                return false;
            }

            return _recipeMap.TryGetValue(viewId, out recipe);
        }

        // 에디터 전용: 동적 추가
        public void Register(IViewRecipe recipe)
        {
            if (_recipeMap == null)
                _recipeMap = new Dictionary<ViewId, IViewRecipe>();

            if (recipe == null || recipe.ViewId == default)
            {
                Debug.LogError("[ViewRecipeRegistry] Invalid recipe");
                return;
            }

            _recipeMap[recipe.ViewId] = recipe;
        }

#if UNITY_EDITOR
        [ContextMenu("Validate All Recipes")]
        private void ValidateRecipes()
        {
            var duplicates = new HashSet<ViewId>();
            var seen = new HashSet<ViewId>();

            foreach (var recipe in recipes)
            {
                if (recipe == null)
                {
                    Debug.LogWarning("[ViewRecipeRegistry] Null recipe in array");
                    continue;
                }

                if (recipe.ViewId == default)
                {
                    Debug.LogWarning($"[ViewRecipeRegistry] Recipe '{recipe.name}' has default ViewId");
                    continue;
                }

                if (seen.Contains(recipe.ViewId))
                {
                    duplicates.Add(recipe.ViewId);
                }
                else
                {
                    seen.Add(recipe.ViewId);
                }
            }

            if (duplicates.Count > 0)
            {
                Debug.LogError($"[ViewRecipeRegistry] Found {duplicates.Count} duplicate ViewIds!");
            }
            else
            {
                Debug.Log($"[ViewRecipeRegistry] Validation passed: {recipes.Length} unique recipes");
            }
        }

        [ContextMenu("Auto-Collect Recipes from Assets")]
        private void AutoCollectRecipes()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ViewRecipe");
            var collected = new List<ViewRecipe>(guids.Length);

            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<ViewRecipe>(path);

                if (recipe != null)
                    collected.Add(recipe);
            }

            recipes = collected.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);

            Debug.Log($"[ViewRecipeRegistry] Auto-collected {recipes.Length} recipes");
        }
#endif
    }
}