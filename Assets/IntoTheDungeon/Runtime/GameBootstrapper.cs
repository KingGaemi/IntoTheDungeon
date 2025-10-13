
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages;


using IntoTheDungeon.Core.Runtime.World;
using System;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Runtime.Abstractions;
using System.Collections.Generic;


namespace IntoTheDungeon.Runtime
{
    public sealed class GameBootstrapper : IDisposable
    {

        public IWorld World => _world;
        public bool IsInitialized { get; private set; }

        readonly ILogger _log;
        readonly IClock _clock;
        readonly ISceneGraph _scene;
        IEventHub _hub;
        readonly List<IGameInstaller> _installers = new();

        GameWorld _world;

        public GameBootstrapper(ILogger log, IClock clock, ISceneGraph scene)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        }

        public void AddInstaller(IGameInstaller installer)
        {
            if (installer != null) _installers.Add(installer);
        }

        public void Init()
        {
            if (IsInitialized) return;

            _world = new GameWorld();
            _world.Set(_log);

            _log.Log("[Bootstrap] Starting installation...");

            // 코어 + 피처 인스톨
            int successCount = 0;
            int failCount = 0;

            for (int i = 0; i < _installers.Count; i++)
            {
                var installer = _installers[i];

#if UNITY_EDITOR
                string installerName = installer != null ? installer.GetType().Name : "null";
                _log.Log($"[Bootstrap] Installing [{i + 1}/{_installers.Count}]: {installerName}");

                try
                {
#endif
                    installer.Install(_world);
                    successCount++;

#if UNITY_EDITOR
                    _log.Log($"[Bootstrap]   ✓ {installerName} installed successfully");
                }
                catch (Exception ex)
                {
                    failCount++;
                    _log.Error($"[Bootstrap]   ✗ {installerName} failed: {ex.Message}");
                }
#endif
            }

            _log.Log($"[Bootstrap] Installation complete: {successCount} succeeded, {failCount} failed");

            if (!_world.TryGet(out _hub))
            {
                _log.Warn("[Bootstrap] EventHub not found in World services.");
            }
            else
            {
                _log.Log("[Bootstrap] EventHub acquired.");
            }


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
            _hub?.ClearFrame();
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