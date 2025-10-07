using UnityEngine;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Core.ECS.Components;
namespace IntoTheDungeon.Core.Runtime.World
{
    public abstract class Baker<T> : IBaker where T : MonoBehaviour
    {
        protected T Authoring { get; private set; }
        protected Entity Entity { get; private set; }
        protected GameWorld World { get; private set; }

        void IBaker.Execute(MonoBehaviour authoring, GameWorld world, Entity entity)
        {
            Entity = entity;
            World = world;
            Bake((T)authoring);
        }
     
        /// <summary>
        /// Baker 내부 초기화 (BakerRunner가 호출)
        /// </summary>
        public void Initialize(T authoring, Entity entity)
        {
            Authoring = authoring;
            Entity = entity;
        }

        /// <summary>
        /// 변환 로직 구현
        /// </summary>
        public abstract void Bake(T authoring);
        

        // ============================================
        // Helper 메서드
        // ============================================

        /// <summary>
        /// Component 추가
        /// </summary>
        protected void AddComponent<TComponent>(TComponent component) where TComponent : struct, IComponentData
        {
            World.EntityManager.AddComponent(Entity, component);
        }

        // / <summary>
        // / Managed Component 추가
        // / </summary>
        protected void AddManagedComponent<TComponent>(TComponent component) where TComponent : class , IManagedComponent
        {
            World.EntityManager.AddManagedComponent(Entity, component);
        }



    

        /// <summary>
        /// 같은 GameObject의 다른 Component 가져오기
        /// </summary>
        protected TComponent GetComponent<TComponent>() where TComponent : Component
        {
            return Authoring.GetComponent<TComponent>();
        }


    }
}