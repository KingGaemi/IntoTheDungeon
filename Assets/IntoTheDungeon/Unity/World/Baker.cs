using System;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.World;
using UnityEngine;

namespace IntoTheDungeon.Unity.World
{
    /// <summary>
    /// Authoring → Entity 변환 베이스 클래스
    /// </summary>
    public abstract class Baker<T> : IBaker where T : IAuthoring
    {
        protected T Authoring { get; private set; }
        protected Entity Entity { get; private set; }
        protected IWorld World { get; private set; }

        // ⭐ EntityManager 직접 접근 (빈번한 호출 최적화)
        protected IEntityManager EM => World.EntityManager;

        public void Execute(IAuthoring authoring, IWorld world, Entity entity)
        {
            var mono = ResolveAuthoring(authoring);

            Authoring = mono;
            World = world;
            Entity = entity;

            // ⭐ 예외 처리 (에디터에서만)
#if UNITY_EDITOR
            try
            {
#endif
                Bake(mono);
#if UNITY_EDITOR
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Baker] Bake failed for {typeof(T).Name}: {ex.Message}",
                    authoring as UnityEngine.Object);
                throw;
            }
#endif
        }

        protected abstract void Bake(T authoring);

        #region Component API

        protected void AddComponent<TComponent>(TComponent component)
            where TComponent : struct, IComponentData
        {
            EM.AddComponent(Entity, component);
        }

        // ⭐ Set 메서드 추가 (AddOrUpdate 패턴)
        protected void SetComponent<TComponent>(TComponent component)
            where TComponent : struct, IComponentData
        {
            if (EM.HasComponent<TComponent>(Entity))
                EM.SetComponent(Entity, component);
            else
                EM.AddComponent(Entity, component);
        }

        protected TComponent GetComponent<TComponent>()
            where TComponent : struct, IComponentData
        {
            return EM.GetComponent<TComponent>(Entity);
        }

        // ⭐ TryGet 패턴 추가
        protected bool TryGetComponent<TComponent>(out TComponent component)
            where TComponent : struct, IComponentData
        {
            if (EM.HasComponent<TComponent>(Entity))
            {
                component = EM.GetComponent<TComponent>(Entity);
                return true;
            }
            component = default;
            return false;
        }

        protected bool HasComponent<TComponent>()
            where TComponent : struct, IComponentData
        {
            return EM.HasComponent<TComponent>(Entity);
        }

        protected void RemoveComponent<TComponent>()
            where TComponent : struct, IComponentData
        {
            EM.RemoveComponent<TComponent>(Entity);
        }

        #endregion
        private static T ResolveAuthoring(IAuthoring a)
        {
            if (a is T ok) return ok;
#if UNITY_EDITOR
            throw new ArgumentException($"Need {typeof(T).Name}, got {a?.GetType().Name}");
#else
            return (T)a; // 릴리즈는 예외 최소화
#endif
        }
        protected TService RequireService<TService>() where TService : class
        {
            if (World.TryGet(out TService s)) return s;
            throw new InvalidOperationException($"Service missing: {typeof(TService).Name}");
        }

    }

}