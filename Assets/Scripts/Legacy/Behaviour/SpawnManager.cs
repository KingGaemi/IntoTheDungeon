#if false

using UnityEngine;
public interface IPartySpawner
{
    PartyCore SpawnParty(PartyBlueprint bp, Transform anchor, System.Random rng);
    void DespawnParty(PartyCore party);
}

public class SpawnManager : MonoBehaviour, IPartySpawner
{
    public PartyCore SpawnParty(PartyBlueprint bp, Transform anchor, System.Random rng)
    {
        // 파티 루트 + PartyCore
        var go = new GameObject($"{bp.team}_Party");
        var core = go.AddComponent<PartyCore>();
        core.SetAnchor(anchor);
        core.placeInWorldSpace = true;
        core.config = ScriptableObject.CreateInstance<PartyConfig>();
        core.teamFlag = bp.team;
        core.config.maxPartySize = bp.maxSize;

        // 멤버 생성/난이도 적용
        foreach (var e in bp.entries)
        {
            int n = Mathf.Max(1, e.count);
            for (int i = 0; i < n; i++)
            {
                var member = Instantiate(e.prefab, anchor.position, Quaternion.identity);
                foreach (var r in member.GetComponentsInChildren<IStatReceiver>(true))
                    r.ApplyDifficulty(bp.difficulty);
                member.SetActive(false);     // 대기 생성
                core.TryAdd(member);
            }
        }
        return core;
    }

    public void DespawnParty(PartyCore party)
    {
        if (!party) return;
        // 풀링 사용 시 반환, 아니면 Destroy
        foreach (var t in party.Members) if (t) Destroy(t.gameObject);
        Destroy(party.gameObject);
    }
}
#endif