// EntityViewMappingTable.cs
using UnityEngine;
using System;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Services;

namespace IntoTheDungeon.Unity.Bridge.View
{
    [CreateAssetMenu(fileName = "EntityViewMappingTable", menuName = "IntoTheDungeon/EntityViewMappingTable")]
    public sealed class EntityViewMappingTable : ScriptableObject
    {
        [Serializable]
        public struct Mapping
        {
            [Tooltip("Entity Recipe ID (e.g., 'Character')")]
            public string recipeIdString;

            [Tooltip("View Recipe to use")]
            public ViewRecipe viewRecipe;
        }

        [SerializeField] private Mapping[] mappings;

        public void ApplyMappings(IEntityViewMapRegistry registry, ViewRecipeRegistry viewRegistry, IRecipeTable recipeTable)
        {
            foreach (var mapping in mappings)
            {
                if (mapping.viewRecipe == null)
                {
                    Debug.LogWarning($"[EntityViewMapping] ViewRecipe null for '{mapping.recipeIdString}'");
                    continue;
                }

                // ViewRecipeRegistry.Initialize()가 이 호출보다 먼저 실행되어야
                // mapping.viewRecipe.ViewId가 유효하다 (UnityCoreInstaller에서 순서 보장).
                var recipeId = recipeTable.GetId(mapping.recipeIdString);
                var viewId = mapping.viewRecipe.ViewId;

                registry.Register(recipeId, viewId);
                Debug.Log($"[EntityViewMapping] {recipeId.Value} → ViewId {viewId.Value}");
            }
        }
    }
}