using System;
using System.Collections.Generic;
using System.Linq;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.ECS.Abstractions;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityRecipeRegistry", menuName = "IntoTheDungeon/Recipes/EntityRecipeRegistry")]
public sealed class EntityRecipeRegistry : ScriptableObject, IRecipeRegistry
{
    [SerializeField] List<ScriptableObject> factoryAssets = new();

    readonly Dictionary<RecipeId, IEntityRecipeFactory> _map = new();

    public IEnumerable<RecipeId> Ids => _map.Keys;

    public void ForceRebuild() => Rebuild();
    void OnEnable() => Rebuild();
#if UNITY_EDITOR
    void OnValidate() => Rebuild();
#endif

    void Rebuild()
    {
        _map.Clear();
        for (int i = 0; i < factoryAssets.Count; i++)
        {
            if (factoryAssets[i] is not IEntityRecipeFactory f)
            { Debug.LogError($"[{name}] #{i} is not IEntityRecipeFactory"); continue; }

            var id = f.RecipeId;
            if (_map.ContainsKey(id))
                Debug.LogWarning($"[{name}] Duplicate RecipeId {id.Value}. Overwriting.");

            _map[id] = f;
        }
    }

    // 런타임 동적 등록도 허용
    public void Register(RecipeId id, IEntityRecipeFactory factory)
    {
        if (factory == null) { Debug.LogError("[EntityRecipeRegistry] Register null"); return; }
        _map[id] = factory;
    }

    public bool TryGetFactory(RecipeId id, out IEntityRecipeFactory factory)
    {
        if (_map.TryGetValue(id, out var f))
        {
            factory = f;
            return true;
        }
        Debug.LogError($"[EntityRecipeRegistry] Recipe not found: {id.Value}");
        factory = null;
        return false;
    }

    public IEntityRecipeFactory GetFactory(RecipeId id)
        => _map.TryGetValue(id, out var f) ? f : throw new KeyNotFoundException($"Recipe not found: {id.Value}");

}