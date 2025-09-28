using UnityEngine;
using UnityEngine.SceneManagement;
using IntoTheDungeon.Core.Runtime.Installation;
using IntoTheDungeon.Core.Runtime.Runner;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Core.Runtime.Installation.Installers;


[DefaultExecutionOrder(-10000)]
public sealed class GameBootstrapper : MonoBehaviour
{

    [Header("Installers")]
    [SerializeField] GameInstallers installers;

    [Header("First Scene")]
    [SerializeField] string firstSceneName = "Main";

    void Awake()
    {
        var existing = FindFirstObjectByType<GameBootstrapper>();
        if (existing && existing != this) { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);

        var world = new GameWorld();
        var runner = new SystemRunner(world);
        var srb = gameObject.AddComponent<SystemRunnerBehaviour>();
        srb.Init(runner);
        world.Runner = runner;

        new CoreGameInstaller().Install(world, runner);
        // 중복 방지

        // 씬 내 모든 IWorldInjectable에 주입 (비활성 포함 권장)
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] is IWorldInjectable inj) inj.Init(world);

    }
}
