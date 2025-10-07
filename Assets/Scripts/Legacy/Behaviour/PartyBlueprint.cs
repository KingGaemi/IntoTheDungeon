#if false

using UnityEngine;
using IntoTheDungeon.Features.Core;



[CreateAssetMenu(menuName = "Party/Blueprint")]
public class PartyBlueprint : ScriptableObject
{
    public TeamFlag team;
    public int maxSize = 4;
    [System.Serializable] public struct Entry { public GameObject prefab; public int count; public float weight; }
    public Entry[] entries;            // 고정 or 확률 조합
    public DifficultyMod difficulty;   // 기본 보정
}

#endif