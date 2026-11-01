using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 정비 화면의 아이템 한 개(가방 안 또는 주운 아이템 칸). 끌어서 옮기고, 끄는 동안 우클릭으로 90도 회전.
    /// 놓는 자리 판정·데이터 변경은 CampHud가 한다.
    /// </summary>
    public sealed class InventoryItemView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public CampHud Owner;
        public ItemDef Def;
        public PlacedItem Placed;   // 격자(장비 칸·보관함) 안이면 그 칸 정보, 주운 아이템 칸이면 null
        public Inventory Source;    // 들어 있는 격자(주운 아이템 칸이면 null)
        public bool Rotated;

        public bool Dragging { get; private set; }
        RectTransform _rt;
        CanvasGroup _group;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _group = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            Dragging = true;
            _group.blocksRaycasts = false;   // 아래 칸·버리기 영역이 포인터를 받게
            Owner.BeginDrag(this);
            Follow(e);
        }

        public void OnDrag(PointerEventData e) => Follow(e);

        public void OnEndDrag(PointerEventData e)
        {
            Dragging = false;
            _group.blocksRaycasts = true;
            Owner.Drop(this, e.position);
        }

        void Update()
        {
            if (Dragging && Input.GetMouseButtonDown(1))
            {
                Rotated = !Rotated;
                Owner.ResizeView(this);
            }
        }

        void Follow(PointerEventData e)
        {
            var parent = (RectTransform)_rt.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out var local))
                _rt.anchoredPosition = local;
        }

        public void OnPointerEnter(PointerEventData e) => Owner.ShowInfo(Def);
        public void OnPointerExit(PointerEventData e) => Owner.ShowInfo(null);
    }
}
