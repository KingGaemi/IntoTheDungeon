#if false
using UnityEngine;


public interface IStageController { void StartStage(StageInfo info); event System.Action StageCleared; bool GenerateStage(); }
public interface IPartyService { PartyCore PlayerParty { get; } }



public enum GameState
{
    None,       // 초기화 안 된 상태 (선택사항)
    Ready,      // 준비 완료 (게임 시작 전 로비/메뉴 상태)
    Playing,    // 실제 플레이 중
    Paused,     // 일시 정지
    Intermission, // 스테이지 클리어 후 정비 시간
    GameOver,   // 패배 상태
    Victory     // 최종 승리
}

public class GameManager : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private StageManager stageControllerRef; // StageManager 컴포넌트
    [SerializeField] private SpawnManager spawnServiceRef;
    [SerializeField] private PartyManager partyManager;
    [SerializeField] private CameraController cameraController;


    [Header("Test")]
    [SerializeField] private StageInfo currentStage;

    IStageController stageManager;
    IPartySpawner spawner;

    public GameState State { get; private set; } = GameState.Ready;

    // 에디터에서 드래그할 때 자동으로 연결
    private void OnValidate()
    {
        if (partyManager == null)
            partyManager = FindFirstObjectByType<PartyManager>();
        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
    }

    // 런타임 보강(실행 중 핫스왑 등 대비)
    private void Awake()
    {
        stageManager = stageControllerRef as IStageController;
        spawner = spawnServiceRef as IPartySpawner;
        Debug.Assert(stageManager != null && spawner != null, "Missing interfaces");

    }


    [ContextMenu("Test/Game Start")]
    public void GameStart()
    {
        if (State != GameState.Ready) { Debug.LogWarning($"Game already {State}"); return; }
        // 예: 세이브 로드, 플레이어 초기화 등
        State = GameState.Playing;
        Debug.Log("Game started");
    }

    [ContextMenu("Test/Stage Start")]
    public void StageStart()
    {
        if (State != GameState.Playing) { Debug.LogWarning("Call GameStart first"); return; }
        if (!currentStage) { Debug.LogWarning("Assign StageInfo"); return; }

        stageManager.StartStage(currentStage);

        Debug.Log($"Stage started: {currentStage.name}");
    }

  
    public bool BuildStage()

    {
        if (stageManager == null)
        {
            Debug.LogError("[GameManager] StageManager is not assigned.");
            return false;
        }

        // StageManager의 BuildStage 실행 / delegate to StageManager
        bool result = stageManager.GenerateStage();

        if (!result)
            Debug.LogWarning("[GameManager] Stage build failed.");

        return result;
    }


    void OnStageCleared()
    {
        State = GameState.Intermission;
        Debug.Log("Stage cleared → Intermission");
        // 정비 UI 열기 등…
    }

}
#endif