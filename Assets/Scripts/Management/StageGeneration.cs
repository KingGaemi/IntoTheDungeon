using UnityEngine;

public class StageGeneration : MonoBehaviour
{
    [SerializeField] GameObject stagePrefab;

    GameObject bossObj;
    Vector3 lastPos = new Vector3(39f, -22f, 0f);

    public int stageLevel = 1;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        bossObj = GameObject.FindGameObjectWithTag("Boss");
        if (bossObj != null)
        {
            // 2. 해당 오브젝트에서 StatusComponent 가져오기
            StatusComponent status = bossObj.GetComponentInChildren<StatusComponent>();

            if (status != null)
            {
                // 3. 이벤트 구독 또는 필요한 처리
                status.OnDeath += OnBossDeath;
            }
            else
            {
                Debug.LogWarning("Boss 오브젝트에 StatusComponent가 없음!");
            }
        }
        else
        {
            Debug.LogWarning("씬에 Boss 태그 오브젝트가 없음!");
        }
        GenerateStage();

    }

    // Update is called once per frame
    void Update()
    {

    }


    public void GenerateStage()
    {

        lastPos = lastPos + new Vector3(39f, -8f, 0f);
        // 프리팹 생성
        Instantiate(stagePrefab, lastPos, Quaternion.identity);


    }

    void OnBossDeath()
    {
        Debug.Log("Round Set");
    }
}
