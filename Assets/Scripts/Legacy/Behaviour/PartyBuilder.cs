#if false
using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Features.Core;

[CreateAssetMenu(menuName = "Party/Build")]

public class PartyBuildRequest : ScriptableObject
{
    public TeamFlag team;
    public GameObject[] memberPrefabs;
}

public class PartyBuilder : MonoBehaviour
{
    [SerializeField] private GameObject defaultPartyPrefab;
    [SerializeField] private PartyConfig partyConfig;  // 멤버 수 제한 등
    [SerializeField] private PartyManager partyManager;
    // [SerializeField] private Queue<PartyBuildRequest> buildQueue = new Queue<PartyBuildRequest>();

    [SerializeField] private List<PartyBuildRequest> testRequests = new();
    private Queue<PartyBuildRequest> buildQueue = new();
    int playerCount = 0, enemyCount = 0;

    void Awake()
    {
        // 테스트용으로 리스트를 큐에 옮김
        foreach (var req in testRequests)
            buildQueue.Enqueue(req);
    }
    void OnValidate()
    {
        if (partyManager == null)
            partyManager = FindFirstObjectByType<PartyManager>();
    }
    public void Enqueue(PartyBuildRequest req)
    {
        buildQueue.Enqueue(req);
    }

    void Update()
    {
        if (buildQueue.Count == 0) return;

        var req = buildQueue.Dequeue();
        BuildParty(req);
    }

    void BuildParty(PartyBuildRequest req)
    {
        // 1) 프리팹 인스턴스 
        var party = Instantiate(defaultPartyPrefab);
        var core = party.GetComponent<PartyCore>();
        if (!core) { Debug.LogError("DefaultPartyPrefab에 PartyCore가 필요합니다."); return; }

        // 2) 이름 부여 (진영별 카운터)
        string baseName = (req.team == TeamFlag.Player) ? "PlayerParty" : "EnemyParty";
        int index = (req.team == TeamFlag.Player) ? ++playerCount : ++enemyCount; party.name = $"{baseName}_{index}";

        // 3) 설정 주입 
        core.SetConfig(partyConfig); // 멤버 수 제한 등 
        core.SetTeamFlag(req.team); // 앵커 깃발 자동 교체용(앞서 구현한 Apply 메서드 사용) 

        for (int i = 0; i < req.memberPrefabs.Length; i++)
        {
            var member = Instantiate(req.memberPrefabs[i], core.MembersContainer, false);
            core.TryAdd(member);
        }
        Facing2D facing = (req.team == TeamFlag.Player) ? Facing2D.Right : Facing2D.Left;
        core.SetFacingDirection(facing);
        if (partyManager) partyManager.RegisterParty(party);
    }

        /// =========================Debug==================
        /// 
        static string PathOf(Transform t)
        {
            if (!t) return "NULL";
            var p = t;
            var sb = new System.Text.StringBuilder(t.name);
            while (p.parent) { p = p.parent; sb.Insert(0, p.name + "/"); }
            return sb.ToString();
        }
}
#endif