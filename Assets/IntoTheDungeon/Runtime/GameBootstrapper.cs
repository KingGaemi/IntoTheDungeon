
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages;

using IntoTheDungeon.Runtime.Installation.Installers;

using IntoTheDungeon.Runtime.Installation;
using IntoTheDungeon.Core.Runtime.World;
using System;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Runtime.Abstractions;
using IntoTheDungeon.Core.Runtime.Event;
using System.Collections.Generic;


namespace IntoTheDungeon.Runtime
{
    public sealed class GameBootstrapper  : IDisposable
    {
        
        public IWorld World => _world;
        public bool   IsInitialized { get; private set; }

        readonly ILogger     _log;
        readonly IClock      _clock;
        readonly ISceneGraph _scene;
        readonly List<IGameInstaller> _installers = new();

        GameWorld _world;
        IEventHub _hub;

        public GameBootstrapper(ILogger log, IClock clock, ISceneGraph scene)
        {
            _log   = log  ?? throw new ArgumentNullException(nameof(log));
            _clock = clock?? throw new ArgumentNullException(nameof(clock));
            _scene = scene?? throw new ArgumentNullException(nameof(scene));
        }

        public void AddInstaller(IGameInstaller installer)
        {
            if (installer != null) _installers.Add(installer);
        }

        public void Init()
        {
            if (IsInitialized) return;

            _world = new GameWorld();

            // 필수 서비스 주입
            _world.Set<ILogger>(_log);
            _world.Set<IClock>(_clock);
            _hub = new EventHub();
            _world.Set<IEventHub>(_hub);

            // 코어 + 피처 인스톨
            new CoreGameInstaller().Install(_world);
            for (int i = 0; i < _installers.Count; i++)
                _installers[i].Install(_world);

            // 씬 주입
            InjectScene();

            IsInitialized = true;
            _log.Log("[Bootstrap] Initialized.");
        }

        public void Dispose()
        {
            if (!IsInitialized) return;
            IsInitialized = false;
            _log.Log("[Bootstrap] Disposed.");
        }

        // 외부 엔진이 호출
        public void Tick(float dt)
        {
            if (!IsInitialized) return;
            _world.SystemManager.ExecuteUpdate(dt);
            _world.SystemManager.ExecuteLate(dt);
            _hub.ClearFrame(); // 프레임 로컬 이벤트 정리
        }

        public void FixedTick(float dt)
        {
            if (!IsInitialized) return;
            _world.SystemManager.ExecuteFixed(dt);
        }

        public void ReinjectScene() => InjectScene();

        void InjectScene()
        {
            foreach (var inj in _scene.EnumerateInjectables())
                inj.Init(_world);
        }
    }

}