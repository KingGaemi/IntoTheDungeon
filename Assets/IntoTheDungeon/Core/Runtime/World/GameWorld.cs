using System.Collections.Generic;
using System;
using IntoTheDungeon.Core.Runtime.ECS.Manager;
namespace IntoTheDungeon.Core.Runtime.World
{
    public sealed class GameWorld : Abstractions.IDisposable
    {
        private readonly EntityManager _entityManager;
        public EntityManager EntityManager => _entityManager;

        private readonly SystemManager _systemManager;
        public SystemManager SystemManager => _systemManager;

        public GameWorld()
        {
            _entityManager = new EntityManager();
            _systemManager = new SystemManager(this); // World 참조 전달

            // 매니저들을 서비스로도 등록 (편의성)
            Set(_entityManager);
            Set(_systemManager);
        }

        private readonly Dictionary<Type, object> _services = new();
        public void Set<T>(T svc) where T : class
            => _services[typeof(T)] = svc;

        public T Require<T>() where T : class
            => (T)_services[typeof(T)];

        public bool TryGet<T>(out T svc) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var v))
            {
                svc = (T)v;
                return true;
            }
            svc = null;
            return false;
        }

        public void Dispose()
        {
            foreach (var svc in _services.Values)
                if (svc is IDisposable d) d.Dispose();
            _services.Clear();
        }
        
    }
}