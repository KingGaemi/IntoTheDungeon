// EntityViewMappingTable.cs
using UnityEngine;
using System;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Features.View
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

        public void ApplyMappings(IEntityViewMapRegistry registry, ViewRecipeRegistry viewRegistry)
        {
            foreach (var mapping in mappings)
            {
                if (mapping.viewRecipe == null)
                {
                    Debug.LogWarning($"[EntityViewMapping] ViewRecipe null for '{mapping.recipeIdString}'");
                    continue;
                }

                var recipeId = new RecipeId(RecipeStringToId(mapping.recipeIdString));
                var viewId = mapping.viewRecipe.ViewId;

                registry.Register(recipeId, viewId);
                Debug.Log($"[EntityViewMapping] {recipeId.Value} → ViewId {viewId.Value}");
            }
        }

        RecipeId RecipeStringToId(in string recipeString)
        {
            return recipeString switch
            {
                "Character" => RecipeIds.Character,
                _ => RecipeIds.Default,
            };
        }


    }
}