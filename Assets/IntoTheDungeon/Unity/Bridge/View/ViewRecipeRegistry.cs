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

        // 런타임 초기화 — ViewId는 여기서 순번으로 발급한다(레시피가 스스로 계산하지 않음).
        public void Initialize()
        {
            _recipeMap = new Dictionary<ViewId, IViewRecipe>(recipes.Length);
            var seenAssets = new HashSet<IViewRecipe>();

            foreach (var recipe in recipes)
            {
                if (recipe == null)
                {
                    Debug.LogWarning($"[ViewRecipeRegistry] Null recipe, skipping");
                    continue;
                }

                if (!seenAssets.Add(recipe))
                {
                    Debug.LogError($"[ViewRecipeRegistry] '{((ScriptableObject)recipe).name}' 에셋이 recipes 배열에 중복 등록되어 있습니다.");
                    continue;
                }

                var viewId = new ViewId(_recipeMap.Count);
                recipe.AssignViewId(viewId);
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

        // 에디터/런타임 동적 추가: 다음 순번을 이 레지스트리가 직접 발급한다.
        public void Register(IViewRecipe recipe)
        {
            if (_recipeMap == null)
                _recipeMap = new Dictionary<ViewId, IViewRecipe>();

            if (recipe == null)
            {
                Debug.LogError("[ViewRecipeRegistry] Invalid recipe");
                return;
            }

            var viewId = new ViewId(_recipeMap.Count);
            recipe.AssignViewId(viewId);
            _recipeMap[viewId] = recipe;
        }

#if UNITY_EDITOR
        // ViewId는 이제 배열 순번으로 발급되므로 값 충돌은 구조적으로 불가능하다.
        // 여기서 잡아야 할 진짜 실수는 "같은 에셋이 배열에 두 번 들어간 경우"뿐.
        [ContextMenu("Validate All Recipes")]
        private void ValidateRecipes()
        {
            var seen = new HashSet<ViewRecipe>();
            int duplicateCount = 0;

            foreach (var recipe in recipes)
            {
                if (recipe == null)
                {
                    Debug.LogWarning("[ViewRecipeRegistry] Null recipe in array");
                    continue;
                }

                if (!seen.Add(recipe))
                {
                    duplicateCount++;
                    Debug.LogError($"[ViewRecipeRegistry] '{recipe.name}' 에셋이 배열에 중복 등록되어 있습니다.");
                }
            }

            if (duplicateCount > 0)
                Debug.LogError($"[ViewRecipeRegistry] Found {duplicateCount} duplicate entries!");
            else
                Debug.Log($"[ViewRecipeRegistry] Validation passed: {recipes.Length} unique recipes");
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