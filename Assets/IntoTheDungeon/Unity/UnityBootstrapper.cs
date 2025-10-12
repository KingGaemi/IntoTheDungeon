using UnityEngine;
using UnityEngine.SceneManagement;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.World.Installation;
using IntoTheDungeon.Runtime.Installation.Installers;
using IntoTheDungeon.World.Implementaion;
using IntoTheDungeon.Runtime.Unity.World;
using System.Linq;

namespace IntoTheDungeon.Runtime
{
    [DefaultExecutionOrder(-10000)]
    public sealed class UnityBootstrapper : MonoBehaviour
    {
        static GameBootstrapper instance;
        static bool isInitialized;

        [Header("Installers")]
        [SerializeField] GameInstallers installers;

        [Header("First Scene")]
        [SerializeField] string firstSceneName = "NewScene";

        // private으로 변경
        GameWorld _world;

        // 명시적 접근 메서드
        public static IWorld GetWorld()
        {
            if (instance == null)
            {
                Debug.LogError("[GameBootstrapper] Instance not found!");
                return null;
            }
            return instance._world;
        }

        // 안전한 접근 (null 체크 포함)
        public static bool TryGetWorld(out IWorld world)
        {
            world = instance != null ? instance._world : null;
            return world != null;
        }


        void Awake()
        {
            if (!InitializeSingleton()) return;

            InitializeCore();
            RegisterSceneCallbacks();
            LoadFirstScene();
        }

        void OnDestroy()
        {
            UnregisterSceneCallbacks();
            Cleanup();
        }

        bool InitializeSingleton()
        {
            if (instance != null)
            {
                if (instance != this)
                {
                    Destroy(gameObject);
                    return false;
                }
                return false; // 이미 초기화됨
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            return true;
        }

        void InitializeCore()
        {
            if (isInitialized) return;

            _world = new GameWorld();
            // Core 설치
            new CoreGameInstaller().Install(_world);

            // 추가 Installer 실행 (ScriptableObject 기반)
            if (installers != null)
                installers.InstallAll(_world);


            _world.BakeScene();
            isInitialized = true;
        }

        void RegisterSceneCallbacks()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void UnregisterSceneCallbacks()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        void LoadFirstScene()
        {
            if (!string.IsNullOrEmpty(firstSceneName))
            {
                // 현재 씬이 Bootstrap 씬이라면 첫 씬으로 전환
                if (SceneManager.GetActiveScene().name != firstSceneName)
                    SceneManager.LoadScene(firstSceneName);
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InjectSceneObjects(scene);
        }

        void OnSceneUnloaded(Scene scene)
        {
            // 필요시 씬별 정리 로직
        }

        void InjectSceneObjects(Scene scene)
        {
            // 해당 씬의 루트 오브젝트만 탐색 (성능 최적화)
            var roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                InjectRecursive(roots[i].transform);
            }
        }

        void InjectRecursive(Transform root)
        {
            // 현재 오브젝트 주입
            var components = root.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IWorldInjectable injectable)
                    injectable.Init(_world);
            }

            // 자식 재귀 처리
            for (int i = 0; i < root.childCount; i++)
            {
                InjectRecursive(root.GetChild(i));
            }

        }


        void Cleanup()
        {
            isInitialized = false;
            instance = null;
        }

        // 디버그용
        [ContextMenu("Force Reinject Current Scene")]
        void ForceReinjectCurrentScene()
        {
            InjectSceneObjects(SceneManager.GetActiveScene());
        }


        void Update()
        {
            _world.SystemManager.ExecuteUpdate(Time.deltaTime);
        }
        void FixedUpdate()
        {
            _world.SystemManager.ExecuteFixed(Time.fixedDeltaTime);
        }

        void LateUpdate()
        {
            _world.SystemManager.ExecuteLate(Time.deltaTime);
        }
    }

}