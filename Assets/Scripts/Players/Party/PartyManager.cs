using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PartyManager : MonoBehaviour
{
    [Serializable]
    public struct Member
    {
        public int entityId;        // ECS가 있다면 연결; 없으면 0/미사용 가능
        public GameObject go;       // 씬/프리팹 인스턴스
    }

    public enum AnchorMode { ManagerTransform, ExternalTransform, FixedWorldPosition }

    [Header("Party Size")]
    [Min(1)] public int maxPartySize = 4;
    [SerializeField] private List<Member> members = new();   // 현재 파티원 (0~Count-1)

    [Header("Anchor")]
    public AnchorMode anchorMode = AnchorMode.ManagerTransform;
    public Transform anchorTransform;       // ExternalTransform일 때 사용(위치만 참조)
    public Vector3 fixedAnchorPosition;     // FixedWorldPosition일 때 사용

    [Header("Layout")]
    [Tooltip("앵커로부터의 간격(원형 반지름 또는 라인 간격 등)")]
    public float spacing = 2f;
    [Tooltip("원형 배치로 간단히 배치 (false면 X축 라인 배치)")]
    public bool useCircle = true;
    [Tooltip("월드좌표로 직접 배치 (false면 PartyManager의 로컬좌표로 배치)")]
    public bool placeInWorldSpace = false;

    [Header("Events")]
    public UnityEvent onPartyFull;
    public UnityEvent<GameObject> onMemberAdded;
    public UnityEvent<GameObject> onMemberRemoved;

    public int CurrentCount => members.Count;
    public IReadOnlyList<Member> Members => members;

    // ────────────────────────────────────────────────────────────────
    // Public API
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 파티에 멤버 추가 시도. 성공시 true. 정원 초과 시 false.
    /// </summary>
    public bool TryAdd(GameObject go, int entityId = 0)
    {
        if (go == null) return false;
        if (members.Count >= maxPartySize)
        {
            Debug.LogWarning("Party is full. Cannot add more members.");
            onPartyFull?.Invoke();
            return false;
        }
        if (Contains(go))
        {
            Debug.LogWarning("Already in party.");
            return false;
        }

        members.Add(new Member { entityId = entityId, go = go });
        onMemberAdded?.Invoke(go);
        Arrange();
        return true;
    }

    /// <summary>
    /// 파티에서 멤버 제거. 성공시 true.
    /// </summary>
    public bool Remove(GameObject go)
    {
        if (go == null) return false;
        int idx = members.FindIndex(m => m.go == go);
        if (idx < 0) return false;

        onMemberRemoved?.Invoke(go);
        members.RemoveAt(idx);
        Arrange();
        return true;
    }

    public bool Contains(GameObject go) => members.Exists(m => m.go == go);

    /// <summary>
    /// 현재 설정(앵커/간격/배치 방식)에 따라 멤버들 위치 재배치.
    /// </summary>
    public void Arrange()
    {
        if (members.Count == 0) return;

        Vector3 anchorWS = GetAnchorPositionWS();

        if (useCircle)
        {
            float step = 360f / Mathf.Max(1, members.Count);
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m.go == null) continue;

                float ang = step * i * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * spacing;
                SetObjectPosition(m.go.transform, anchorWS + offset);
            }
        }
        else
        {
            // X축 라인 배치 (0, +spacing, +2*spacing, …)
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m.go == null) continue;

                Vector3 offset = new Vector3(i * spacing, 0f, 0f);
                SetObjectPosition(m.go.transform, anchorWS + offset);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────

    private Vector3 GetAnchorPositionWS()
    {
        switch (anchorMode)
        {
            case AnchorMode.ExternalTransform:
                return anchorTransform ? anchorTransform.position : transform.position;
            case AnchorMode.FixedWorldPosition:
                return fixedAnchorPosition;
            default:
                return transform.position;
        }
    }

    private void SetObjectPosition(Transform t, Vector3 targetWS)
    {
        if (placeInWorldSpace)
        {
            t.position = targetWS;
        }
        else
        {
            // PartyManager의 로컬좌표로 두고 싶을 때
            t.SetParent(transform, worldPositionStays: true);
            t.localPosition = transform.InverseTransformPoint(targetWS);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 p = Application.isPlaying ? GetAnchorPositionWS()
                   : (anchorMode == AnchorMode.ManagerTransform ? transform.position
                      : anchorMode == AnchorMode.ExternalTransform && anchorTransform
                        ? anchorTransform.position
                        : fixedAnchorPosition);
        Gizmos.DrawSphere(p, 0.12f);
        Gizmos.DrawWireSphere(p, spacing);
    }
#endif
}
