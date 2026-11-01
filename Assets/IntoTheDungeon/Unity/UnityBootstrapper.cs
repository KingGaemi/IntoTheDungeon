using UnityEngine;
using UnityEngine.SceneManagement;
using IntoTheDungeon.Core.Abstractions.World;
using System.Collections.Generic;
using IntoTheDungeon.Runtime;
using IntoTheDungeon.Core.Runtime.Event;
using IntoTheDungeon.Unity.World;
using IntoTheDungeon.Unity.Bridge.Physics;
using IntoTheDungeon.Unity.Bridge.Physics.Abstractions;
using IntoTheDungeon.Unity.Bridge.View.Abstractions;
using IntoTheDungeon.Unity.Bridge.View;

namespace IntoTheDungeon.Unity
{

    [DefaultExecutionOrder(-10000)]
    public sealed class UnityBootstrapper : MonoBehaviour
    {
        [Tooltip("씬에 배치된 설치자(MonoGameInstaller)들을 자동 수집")]
        [SerializeField] bool autoCollectInstallers = true;

        [Tooltip("명시적으로 등록할 설치자들")]
        [SerializeField] List<MonoGameInstaller> installers = new();


#if UNITY_EDITOR
        public static event System.Action<IWorld> WorldChanged; // null이면 파괴 의미
#endif


        GameBootstrapper _bootstrap;
        ViewBridge _viewBridge;
        PhysicsBridge _physicsBridge;
        public IWorld World => _bootstrap?.World;

        UnitySceneGraph _sceneGraph;
        UnityClock _clock;
        UnityLogger _logger;
        EventHub _hub;

        void Awake()
        {
            Debug.Log("[Bootstrapper] Awake started");

            // 서비스 구성
            var sceneGraph = new UnitySceneGraph();
            var clock = new UnityClock();
            var logger = new UnityLogger();


            // EventHub는 Installer에서 등록하도록 변경
            _bootstrap = new GameBootstrapper(logger, clock, sceneGraph);


            if (autoCollectInstallers)
            {
                var found = FindObjectsByType<MonoGameInstaller>(FindObjectsSortMode.None);
                Debug.Log($"[Bootstrapper] Found {found.Length} installers");

                foreach (var i in found)
                {
                    if (!installers.Contains(i))
                    {
                        installers.Add(i);
                        Debug.Log($"[Bootstrapper] Added: {i.GetType().Name}");
                    }
                }
            }

            Debug.Log($"[Bootstrapper] Total installers: {installers.Count}");

            foreach (var i in installers)
                _bootstrap.AddInstaller(i);

            Debug.Log("[Bootstrapper] Calling Init()");
            _bootstrap.Init();


            Debug.Log("[Bootstrapper] Calling BakeScene()");
            World.BakeScene();

            // 전투 화면 UI(필살기 버튼·웨이브 진행·던전 지도·정비)와 투사체(검기·화살)·체력바·데미지 숫자·떨어진 아이템 그림: 씬 배치 없이 코드로 만든다(주입 전에 만들어야 Init을 받음)
            new GameObject("BattleHud", typeof(Hud.SkillButtonHud), typeof(Hud.ProjectileRenderer), typeof(Hud.HealthBarRenderer), typeof(Hud.DamageNumberRenderer), typeof(Hud.LootRenderer), typeof(Hud.RewardFxRenderer), typeof(Hud.BossRenderer), typeof(Hud.SpeedControlHud), typeof(Hud.BattleStatusHud), typeof(Hud.DungeonMapHud), typeof(Hud.CampHud));

            Debug.Log("[Bootstrapper] Calling ReinjectScene()");
            _bootstrap.ReinjectScene();



#if DEBUG
            DebugPrintWorldState();
#endif

#if UNITY_EDITOR
            WorldChanged?.Invoke(World);
#endif

            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            CreatePhysicsBridge();
            World.Set<IPhysicsPort>(_physicsBridge);
            _physicsBridge.Init(World);


            CreateViewBridge();
            World.Set<IViewPort>(_viewBridge);
            _viewBridge.Init(World);

            Debug.Log("[Bootstrapper] Awake completed");
        }
#if DEBUG
        void DebugPrintWorldState()
        {
            Debug.Log("=== World State ===");
            Debug.Log($"Entities: {World.EntityManager.EntityCount}");
            Debug.Log($"Systems: {World.SystemManager.Systems.Count}");

            foreach (var sys in World.SystemManager.Systems)
            {
                var enabled = sys.Enabled ? "✓" : "✗";
                Debug.Log($"  [{enabled}] {sys.GetType().Name}");
            }


        }
#endif
        void Start()
        {

        }

        void CreateViewBridge()
        {
            GameObject bridgeGO;



            bridgeGO = new GameObject("[ViewBridge]");
            bridgeGO.transform.position = new Vector3(0, 0, 0);
            _viewBridge = bridgeGO.AddComponent<ViewBridge>();
            // 월드는 씬마다 새로 만들므로 다리(와 그 밑에 스폰된 캐릭터 그림)도 씬과 함께 사라져야 한다.
            // DontDestroyOnLoad면 방을 옮길 때 이전 방의 오크·파티 그림이 남아 겹쳐 보인다.


            if (_viewBridge == null)
            {
                Debug.LogError("[Bootstrapper] ViewBridge component not found!");
                return;
            }

            Debug.Log("[Bootstrapper] ViewBridge created");
        }
        void CreatePhysicsBridge()
        {
            GameObject bridgeGO;



            bridgeGO = new GameObject("[PhysicsBridge]");
            bridgeGO.transform.position = new Vector3(0, 0, 0);
            _physicsBridge = bridgeGO.AddComponent<PhysicsBridge>();


            if (_physicsBridge == null)
            {
                Debug.LogError("[Bootstrapper] PhysicsBridge component not found!");
                return;
            }

            Debug.Log("[Bootstrapper] PhysicsBridge created");
        }
        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
#if UNITY_EDITOR
            WorldChanged?.Invoke(null);
#endif
            _bootstrap?.Dispose();
            _bootstrap = null;
            // 씬이 내려가며 함께 지워지지만, 부트스트래퍼만 따로 지워지는 경우에도 이전 월드 그림이 남지 않게
            if (_viewBridge) Destroy(_viewBridge.gameObject);
            if (_physicsBridge) Destroy(_physicsBridge.gameObject);
        }

        void Update()
        {
            // 외부 엔진 틱 연결
            _bootstrap?.Tick(Time.deltaTime);
        }

        void FixedUpdate()
        {
            _bootstrap?.FixedTick(Time.fixedDeltaTime);
        }

        void OnActiveSceneChanged(Scene _, Scene __)
        {
            // 새 씬의 IInjectable 재주입
            _bootstrap?.ReinjectScene();
        }

    }
}