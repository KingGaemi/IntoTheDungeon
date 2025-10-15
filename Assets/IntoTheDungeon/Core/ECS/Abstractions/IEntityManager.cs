// Core.ECS.Abstractions
using IntoTheDungeon.Core.Abstractions.World;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityManager : Core.Abstractions.IDisposable
    {
        int EntityCount { get; }

        // 엔티티 수명
        Entity CreateEntity();
        Entity CreateEntity(IEntityRecipe recipe);
        Entity CreateEntity(params System.Type[] componentTypes);
        Entity Instantiate(Entity src);
        void DestroyEntity(Entity e);
        bool Exists(Entity e);

        // 컴포넌트
        bool HasComponent<T>(Entity e) where T : struct, IComponentData;
        ref T GetComponent<T>(Entity e) where T : struct, IComponentData;
        void SetComponent<T>(Entity e, T c) where T : struct, IComponentData;
        bool TryGetComponent<T>(Entity e, out T c) where T : struct, IComponentData;
        void AddComponent<T>(Entity e) where T : struct, IComponentData;
        void AddComponent<T>(Entity e, T c) where T : struct, IComponentData;
        void RemoveComponent<T>(Entity e) where T : struct, IComponentData;

        // 쿼리(구현 세부 노출 최소화)
        System.Collections.Generic.IEnumerable<IChunk> GetChunks(params System.Type[] componentTypes);

        IViewOpQueue ViewOps { get; }
    }
}
