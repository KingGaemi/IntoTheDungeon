using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public class CharacterMeta : MonoBehaviour
{
    public string internalId;   // "orc1_basic"
    public string displayName;  // "Orc1_1"
}

public class InformationComponent : MonoBehaviour
{
    [SerializeField] private string internalId;   // 불변 키
    [SerializeField] private string displayName;
    public string Id => internalId;
    public string DisplayName => displayName;

    void OnEnable()  => InfoIndex.Register(this);
    void OnDisable() => InfoIndex.Unregister(this);

    public bool IsSameType(InformationComponent other) => other && other.internalId == internalId;
    public bool Matches(string id) => !string.IsNullOrEmpty(id) && internalId == id;
}

public static class InfoIndex
{
    static readonly Dictionary<string, HashSet<InformationComponent>> byId = new();

    public static void Register(InformationComponent ic)
    {
        if (!ic || string.IsNullOrEmpty(ic.Id)) return;
        if (!byId.TryGetValue(ic.Id, out var set)) set = byId[ic.Id] = new();
        set.Add(ic);
    }
    public static void Unregister(InformationComponent ic)
    {
        if (!ic || string.IsNullOrEmpty(ic.Id)) return;
        if (byId.TryGetValue(ic.Id, out var set)) set.Remove(ic);
    }

    public static IReadOnlyCollection<InformationComponent> AllOf(string id) =>
        byId.TryGetValue(id, out var set) ? (IReadOnlyCollection<InformationComponent>)set : System.Array.Empty<InformationComponent>();

    public static IEnumerable<InformationComponent> InParty(string id, PartyCore party) =>
        AllOf(id).Where(ic => ic && ic.GetComponentInParent<PartyCore>() == party);

    public static InformationComponent Nearest(string id, Vector3 pos)
    {
        var list = AllOf(id);
        InformationComponent best = null; float bestSqr = float.PositiveInfinity;
        foreach (var ic in list)
        {
            var d = (ic.transform.position - pos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = ic; }
        }
        return best;
    }
}
