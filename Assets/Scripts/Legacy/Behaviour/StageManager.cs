#if false
using System;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;

[CreateAssetMenu(menuName = "Stage/StageInfo")]
public class StageInfo : ScriptableObject
{
    public int depth;

    public int globalDifficulty;

    public SpawnPool spawnPool;
}

public class StageManager : MonoBehaviour, IStageController
{
    [Header("Refs")]
    [SerializeField] private PartyCore currentEnemyParty;
    [SerializeField] private SpawnManager spawnManager;
    [SerializeField] private SpawnPool spawnPool;

    [Header("Stage Rule")]

    [SerializeField] private StageInfo stageInfo;
    [SerializeField] private Transform[] spawnPoints; 
    [SerializeField]  Vector3 endPoint;


    public event System.Action StageCleared;

    GameObject bossObj;

    public GameObject roomPrefab;
    public GameObject firstFloor;
    Vector3 startPoint;

    [Header("Difficulty")]
    [SerializeField] private DifficultyMod baseMod = new() { hpMul=1.0f, atkMul=1.0f, spdMul=1.0f, flatHp=0 };
    System.Random rng;
    public Transform startingPoint;
    public GameObject playerParty;
    public int stageLevel = 1;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rng = new System.Random(); // 시드 고정하고 싶으면 넘겨도 됨
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    public bool GenerateStage()
    {
        startPoint = endPoint;
        GameObject go = Instantiate(firstFloor, startPoint, Quaternion.identity);

        Transform newEnd = go.transform.Find("EndPoint");

        if (newEnd != null)
        {
            // 4) 이번에 생성된 스테이지의 EndPoint 위치 저장
            endPoint = newEnd.position;
        }
        else
        {
            Debug.LogWarning("EndPoint child not found in stage prefab!");
            return false;
        }
        return true;
    }




    // Room을 생성하고 Room에 배치될 몬스터들을 SpawnEnemy를 통해 생성한다.
    // roomPrefab, startPoint 가 필요하다
    public void GenerateRoom()
    {
        // 사전 검증: roomPrefab / startPoint 미지정 시 중단
        // Pre-checks: abort if roomPrefab or startPoint is not assigned
        if (roomPrefab == null)
        {
            UnityEngine.Debug.LogWarning("[StageManager] roomPrefab is not assigned.");
            return;
        }
        if (startPoint == null)
        {
            UnityEngine.Debug.LogWarning("[StageManager] startPoint is not assigned.");
            return;
        }

        UnityEngine.GameObject roomInstance = null;

        // 1) 룸 생성 / Room instantiation
        try
        {
            roomInstance = UnityEngine.Object.Instantiate(
                roomPrefab,
                startPoint,
                Quaternion.identity
            );

            UnityEngine.Debug.Log("[StageManager] Room instantiated at startPoint.");
        }
        catch (System.Exception e)
        {
            // 생성 실패
            // Instantiation failed
            UnityEngine.Debug.LogError($"[StageManager] Failed to instantiate room: {e.Message}");
            return;
        }

        // 2) 적 스폰 / Enemy spawn
        bool spawnSucceeded = true; // 반환형을 가정하지 않음; 예외 발생 시 실패로 간주
        try
        {
            // SpawnEnemy 호출 (반환값을 가정하지 않음)
            // Call SpawnEnemy (do not assume return type)
            SpawnEnemy();
            UnityEngine.Debug.Log("[StageManager] SpawnEnemy completed.");
        }
        catch (System.Exception e)
        {
            spawnSucceeded = false;
            UnityEngine.Debug.LogError($"[StageManager] SpawnEnemy failed: {e.Message}");
        }

        // 3) onReady 설정 (존재할 때만) / Set onReady only if it exists
        if (roomInstance != null)
        {
            // 리플렉션으로 'onReady' 필드/프로퍼티 존재 시 true로 설정
            // Use reflection to set 'onReady' to true if such field/property exists
            try
            {
                var behaviours = roomInstance.GetComponents<UnityEngine.MonoBehaviour>();
                foreach (var b in behaviours)
                {
                    var t = b.GetType();

                    var field = t.GetField(
                        "onReady",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic
                    );
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        field.SetValue(b, true);
                        UnityEngine.Debug.Log("[StageManager] Room.onReady (field) set to true.");
                        goto LogDone; // 한 번만 설정
                    }

                    var prop = t.GetProperty(
                        "onReady",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic
                    );
                    if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
                    {
                        prop.SetValue(b, true, null);
                        UnityEngine.Debug.Log("[StageManager] Room.onReady (property) set to true.");
                        goto LogDone; // 한 번만 설정
                    }
                }

                // onReady 멤버가 존재하지 않는 경우 정보 로그
                // Inform if no onReady member is present
                UnityEngine.Debug.Log("[StageManager] No 'onReady' member found on room instance.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[StageManager] Unable to set 'onReady': {e.Message}");
            }
        }

    LogDone:
        // 4) 전체 완료 메시지 / Final status message
        if (spawnSucceeded)
        {
            UnityEngine.Debug.Log("[StageManager] GenerateRoom pipeline completed successfully.");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[StageManager] Room created, but SpawnEnemy encountered an error.");
        }
    }


    public void SpawnEnemy()
    {

    }

    public void StageClear()
    {

    }
    public void StartStage(StageInfo info)
    {
        Vector3 pos = startingPoint.position;// bring or summon Player Party to location
        Debug.Log("StartStage");
    }

    void OnBossDeath()
    {
        Debug.Log("Round Set");
    }
}
#endif