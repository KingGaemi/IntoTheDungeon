using System;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.World;
using UnityEngine;

namespace IntoTheDungeon.Unity.World
{
    public abstract class UnityBaker<TMono> : IBaker where TMono : MonoBehaviour
    {
        protected TMono Authoring { get; private set; } = null!;
        protected Entity Entity { get; private set; }
        protected IWorld World { get; private set; } = null!;

        public void Execute(IAuthoring authoring, IWorld world, Entity entity)
        {
            var mono = ResolveAuthoring(authoring);

            Authoring = mono;
            World = world;
            Entity = entity;

            Bake(mono);
        }

        protected abstract void Bake(TMono authoring);

        protected void AddComponent<TComponent>(TComponent component) where TComponent : struct, IComponentData
        {
            World.EntityManager.AddComponent(Entity, component);
        }

        protected void AddManagedComponent<TComponent>(TComponent component) where TComponent : class, IManagedComponent
        {
            if (World.ManagedStore == null)
            {
                throw new InvalidOperationException("World does not provide a managed component store.");
            }

            World.ManagedStore.AddManagedComponent(Entity, component);
        }

        protected TComponent GetComponent<TComponent>() where TComponent : Component
        {
            return Authoring.GetComponent<TComponent>();
        }

        private static TMono ResolveAuthoring(IAuthoring authoring)
        {
            if (authoring is TMono direct)
                return direct;

            if (authoring is UnityAuthoring wrapper && wrapper.Behaviour is TMono wrapped)
                return wrapped;

            throw new ArgumentException(
                $"Expected authoring component of type {typeof(TMono).Name}, but received {authoring.GetType().Name}.",
                nameof(authoring));
        }
    }
}
