using System;
using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages;

namespace IntoTheDungeon.Core.Runtime.World
{
    public sealed class GameWorld : IWorld
    {
        private readonly EntityManager _entityManager;
        private readonly SystemManager _systemManager;

        IEntityManager IWorld.EntityManager => _entityManager;
        ISystemManager IWorld.SystemManager => _systemManager;

        public IEntityManager EntityManager => _entityManager;
        public ISystemManager SystemManager => _systemManager;

        private readonly Dictionary<Type, object> _services;
        bool _initialized;
        public void MarkInitialized() => _initialized = true;
        public GameWorld()
        {
            _services = new Dictionary<Type, object>();
            _entityManager = new EntityManager();
            _systemManager = new SystemManager(this);


            // 서비스 이중 등록
            Set<EntityManager>(_entityManager);
            Set<IEntityManager>(_entityManager);

            Set<SystemManager>(_systemManager);
            Set<ISystemManager>(_systemManager);


        }

        public void Set<T>(T svc) where T : class
        {
            _services[typeof(T)] = svc;
        }

        public T Require<T>() where T : class
        {
            return (T)_services[typeof(T)];
        }

        public bool TryGet<T>(out T svc) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj)) { svc = (T)obj; return true; }
            svc = null!;
            if (_initialized && _services.TryGetValue(typeof(ILogger), out var lo) && lo is ILogger log)
                log.Warn($"[GameWorld] Service not found: {typeof(T).FullName}");
            return false;
        }

        public void Dispose()
        {
            foreach (var svc in _services.Values)
            {
                if (svc is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _services.Clear();
        }
    }
}
