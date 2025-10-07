using UnityEngine;

[CreateAssetMenu(menuName="Stage/SpawnPool")]
public class SpawnPool : ScriptableObject
{
    [System.Serializable]
    public struct Entry {
        public GameObject prefab;
        [Min(0.0001f)] public float weight;     // 비율(가중치)
    }

    public Entry[] entries;

    public GameObject Pick(System.Random rng = null)
    {
        if (entries == null || entries.Length == 0) return null;
        rng ??= new System.Random();

        // 누적 가중치
        double total = 0;
        foreach (var e in entries) total += e.weight;

        double r = rng.NextDouble() * total;
        foreach (var e in entries)
        {
            r -= e.weight;
            if (r <= 0) return e.prefab;
        }
        return entries[entries.Length - 1].prefab;
    }
}


[System.Serializable]
public struct DifficultyMod
{
    public float hpMul;      // 1.2f = +20%
    public float atkMul;
    public float spdMul;
    public int   flatHp;     // +50 같은 상수 보정
}

public interface IStatReceiver   // 스탯을 세팅/보정할 수 있는 캐릭터 측 인터페이스
{
    void ApplyDifficulty(DifficultyMod mod);
}
