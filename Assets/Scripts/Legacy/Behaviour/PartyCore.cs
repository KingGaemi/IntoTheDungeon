#if false
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using MyGame.GamePlay.Party.Abstractions;
using IntoTheDungeon.Features.Core;
using IntoTheDungeon.Features.Core.Components;

[CreateAssetMenu(menuName = "Party/Config")]
public class PartyConfig : ScriptableObject
{
    public int maxPartySize = 4;
    public float spacing = 2f;
    public bool useCircle = true;
}

public interface IPartyController
{
    void Initialize(PartyCore core);
    void Tick(float dt);
}

[DisallowMultipleComponent]
public class PartyCore : MonoBehaviour, IPartyOrderSource 
{
    [Header("Party Events")]
    public UnityEvent<GameObject> onMemberDied;   // 죽은 멤버 GameObject / dead member
    public UnityEvent onPartyWiped;               // 전멸 / party wiped
    public UnityEvent<PartyOrder> onPartyOrder;

    public event Action<PartyOrder> Order;
    private readonly Dictionary<StatusComponent, System.Action> _deathHandlers = new();

    [Header("Wiring")]
    [SerializeField] private Transform membersContainer; // "Members" 빈 오브젝트 / empty container
    [SerializeField] private List<Transform> members = new(); // 런타임 멤버 / runtime members

    [Header("Config")]
    public PartyConfig config;
    public TeamFlag teamFlag = TeamFlag.Player;

    [Header("Anchor")]
    [SerializeField] private Transform anchor;
    [SerializeField] private SpriteRenderer anchorRenderer;  // Anchor의 SR
    [SerializeField] private Sprite flagAlly;                // flag_ally
    [SerializeField] private Sprite flagEnemy;
    public Vector3 fixedAnchorPosition;
    public bool placeInWorldSpace = false;

    [Header("State")]
    [SerializeField] private bool partyEnabled = true;   // 활성 상태 / party enabled
    [SerializeField] private bool invisible = false;     // 가시성 토글 / visibility toggle

    public bool PartyEnabled => partyEnabled;
    public bool Invisible => invisible;

    public IReadOnlyList<Transform> Members => members;
    public Transform MembersContainer => membersContainer;
    public UnityEvent<GameObject> onMemberAdded;
    public UnityEvent<GameObject> onMemberRemoved;
    public UnityEvent onPartyFull;

    IPartyController controller;

    void Reset() { AutoWire(); SyncFromContainer(); }

    
    void OnValidate()
    {
        if (!Application.isPlaying) SyncFromContainer();

        // 비활성 전환 시 자동으로 보이지 않게 / auto-hide on disable
        if (!partyEnabled) invisible = true;

        if (!anchor) anchor = transform.Find("Anchor");
        if (!anchorRenderer && anchor) anchorRenderer = anchor.GetComponent<SpriteRenderer>();
        
        ApplyTeamFlagSprite();
        ApplyVisibility();
    }

    void AutoWire()
    {
        if (!membersContainer)
            membersContainer = transform.Find("Members");
        if (!membersContainer)
        {
            var go = new GameObject("Members");
            go.transform.SetParent(transform, false);
            membersContainer = go.transform;
        }
    }

    [ContextMenu("Sync From Container")]
    public void SyncFromContainer()
    {
        members.Clear();
        if (!membersContainer) return;
        foreach (Transform child in membersContainer)
            if (child) members.Add(child);
    }

    [ContextMenu("Rebuild Container From List")]
    public void RebuildContainerFromList()
    {
        if (!membersContainer) AutoWire();
        for (int i = membersContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(membersContainer.GetChild(i).gameObject);

        foreach (var t in members)
            if (t) t.SetParent(membersContainer, true);
    }

    void Awake()
    {
        controller = GetComponent<IPartyController>();
        controller?.Initialize(this);
        AutoWire();

    }

    void Start()
    {
        ApplyVisibility();
    }

    void Update()
    {
        // 활성 상태에서만 동작 / run only when enabled
        if (!partyEnabled) return;
        controller?.Tick(Time.deltaTime);
    }

    // ── Public controls ─────────────────────────────────────────────────────
    public void SetPartyEnabled(bool value)
    {
        partyEnabled = value;
        // 비활성화 시 자동으로 숨김 / auto-hide when disabled
        invisible = !value;
        ApplyVisibility();
    }

    public void SetInvisible(bool value)
    {
        invisible = value;
        ApplyVisibility();
    }

    public void SetTeamFlag(TeamFlag team)
    {
        teamFlag= team;
        ApplyTeamFlagSprite();
    }
    public void SetConfig(PartyConfig config)
    {
        this.config = config;
    }

    public void SetAnchor(Transform anchor)
    {
        this.anchor = anchor;
    }

    public void SetFacingDirection(Facing2D dir)
    {
        PartyOrder order = (dir == Facing2D.Left) 
            ? PartyOrder.Left 
            : PartyOrder.Right;

        onPartyOrder?.Invoke(order);
    }
        

    // ── Core API ────────────────────────────────────────────────────────────
    public bool TryAdd(GameObject go)
    {
        if (!go) return false;
        if (members.Count >= config.maxPartySize) { onPartyFull?.Invoke(); return false; }
        var t = go.transform;
        // if (!membersContainer)
        // {
        //     Debug.LogError("MembersContainer가 없습니다."); 
        //     return false;
        // }
        // if (t.parent != membersContainer)
        //     t.SetParent(membersContainer, false); // 로컬 트랜스폼 유지

        if (members.Contains(t)) return false;


        members.Add(t);
        HookDeath(t);
        onMemberAdded?.Invoke(go);
        Arrange();
        

        return true;
    }

    public bool Remove(GameObject go)
    {
        if (!partyEnabled) return false;                    // 비활성 상태에서는 동작 안 함 / no-op when disabled
        if (!go) return false;
        var t = go.transform;
        if (!members.Remove(t)) return false;

        UnhookDeath(t);
        onMemberRemoved?.Invoke(go);
        Arrange();
        return true;
    }

    public void Arrange()
    {
        if (!partyEnabled) return;                          // 비활성 상태에서는 이동 금지 / no arrange when disabled
        if (members.Count == 0) return;
        Vector3 anchor = GetAnchorWS();

        if (config.useCircle)
        {
            float step = 360f / members.Count;
            for (int i = 0; i < members.Count; i++)
            {
                var t = members[i];
                if (!t) continue;
                float a = step * i * Mathf.Deg2Rad;
                Vector3 target = anchor + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * config.spacing;
                SetPos(t, target);
            }
        }
        else
        {
            for (int i = 0; i < members.Count; i++)
            {
                var t = members[i];
                if (!t) continue;
                Vector3 target = anchor + new Vector3(i * config.spacing, 0, 0);
                SetPos(t, target);
            }
        }
    }

    // ── Visibility ──────────────────────────────────────────────────────────
    void ApplyVisibility()
    {
        // 보이는 조건: 활성 상태 && invisible == false / visible only if enabled and not invisible
        bool shouldBeVisible = partyEnabled && !invisible;

        // 본체 렌더러 / renderers on PartyCore
        ToggleRenderers(gameObject, shouldBeVisible);
        ToggleUI(gameObject, shouldBeVisible);              // 본체 하위 UI도 함께 / also toggle UI under root

        if (membersContainer)
        {
            ToggleRenderers(membersContainer.gameObject, shouldBeVisible);
            ToggleUI(membersContainer.gameObject, shouldBeVisible);
        }

        foreach (var t in members)
        {
            if (!t) continue;
            ToggleRenderers(t.gameObject, shouldBeVisible);
            ToggleUI(t.gameObject, shouldBeVisible);        // 각 멤버의 HPBar/Nameplate 등 UI 숨김
        }
    }

    static void ToggleRenderers(GameObject root, bool visible)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;
    }

    static void ToggleUI(GameObject root, bool visible)
    {
        // Canvas 자체 / canvases
        var canvases = root.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
            canvases[i].enabled = visible;

        // CanvasGroup은 투명도/인터랙션 제어 / canvas group for alpha & interaction
        var groups = root.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].alpha = visible ? 1f : 0f;
            groups[i].interactable = visible;
            groups[i].blocksRaycasts = visible;
        }

        // UGUI 그래픽(Text, Image 등) / UGUI graphics
        var graphics = root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].enabled = visible;

#if TMP_PRESENT
        // TextMeshPro 컴포넌트 사용 시 / if TMP present
        var tmpTexts = root.GetComponentsInChildren<TMPro.TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
            tmpTexts[i].enabled = visible;
#endif
    }


    void ApplyTeamFlagSprite()
    {
        if (!anchorRenderer) return;
        anchorRenderer.sprite = (teamFlag == TeamFlag.Player) ? flagAlly : flagEnemy;
    }

    // ── Death handling ──────────────────────────────────────────────────────
    void HookDeath(Transform member)
    {
        var sc = member.GetComponentInChildren<StatusComponent>(true);
        if (!sc || _deathHandlers.ContainsKey(sc)) return;

        System.Action handler = () => HandleMemberDeath(member);
        sc.OnDeath += handler;
        _deathHandlers[sc] = handler;
    }

    void UnhookDeath(Transform member)
    {
        var sc = member ? member.GetComponentInChildren<StatusComponent>(true) : null;
        if (!sc) return;
        if (_deathHandlers.TryGetValue(sc, out var handler))
        {
            sc.OnDeath -= handler;
            _deathHandlers.Remove(sc);
        }
    }

    void HandleMemberDeath(Transform member)
    {
        if (!member) return;

        onMemberDied?.Invoke(member.gameObject);

        members.Remove(member);
        UnhookDeath(member);
        Arrange();

        if (IsPartyWiped())
            onPartyWiped?.Invoke();
    }

    bool IsPartyWiped()
    {
        if (members.Count == 0) return true;

        foreach (var t in members)
        {
            var sc = t ? t.GetComponentInChildren<StatusComponent>(true) : null;
            if (sc && !sc.isDead) return false;
        }
        return true;
    }

    Vector3 GetAnchorWS()
    {
        if (anchor) return anchor.position;
        if (fixedAnchorPosition != default) return fixedAnchorPosition;
        return transform.position;
    }

    void SetPos(Transform t, Vector3 world)
    {
        if (placeInWorldSpace) t.position = world;
        else
        {
            t.SetParent(membersContainer, true);
            t.localPosition = membersContainer.InverseTransformPoint(world);
        }
    }

 

}
#endif