using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 정비 화면의 상점: 왼쪽 위에 늘 펼쳐진 패널(5칸 × 2줄). 그 아래가 파티 상세.
    /// 산 아이템은 주운 아이템 칸으로 들어가므로 끌어서 장비 칸·보관함에 넣는다. 팔기는 아이템을 "팔기" 칸에 놓는다.
    /// </summary>
    public sealed partial class CampHud
    {
        const int ShopTiles = 10;   // 5열 × 2줄(상점 방 최대 품목 수)

        sealed class ShopTile
        {
            public Image Frame, Body, Icon;
            public Text Kind, Title, Desc, PriceLabel;
            public Button Buy;
            public Image BuyImage;
            public ShopOffer Offer;
        }

        readonly ShopTile[] _shopTiles = new ShopTile[ShopTiles];
        GameObject _shopPanel;
        Text _shopTitle, _shopMessage;

        void UpdateShop(RunData run)
        {
            _shopTitle.text = run.InShopRoom ? $"상점 (20% 할인)   ·   골드 {run.Gold}" : $"떠돌이 상인   ·   골드 {run.Gold}";
            for (int i = 0; i < _shopTiles.Length; i++)
            {
                var tile = _shopTiles[i];
                var offer = i < run.Shop.Count ? run.Shop[i] : null;
                tile.Offer = offer;
                if (tile.Frame.gameObject.activeSelf != (offer != null)) tile.Frame.gameObject.SetActive(offer != null);
                if (offer == null) continue;

                bool item = offer.Kind == ShopOfferKind.Item;
                tile.Frame.color = item ? LootRenderer.RarityColor(offer.Item.Rarity)
                                 : offer.Kind == ShopOfferKind.Recruit ? recruitColor
                                 : new Color(1f, 1f, 1f, 0.3f);
                tile.Kind.text = ShopPool.KindLabel(offer);
                tile.Title.text = ShopPool.Title(offer);
                tile.Desc.text = ShopPool.Description(offer);
                var sprite = item ? ItemIconTable.Icon(offer.Item.Id) : null;
                tile.Icon.sprite = sprite;
                tile.Icon.enabled = sprite;
                tile.Desc.rectTransform.offsetMax = new Vector2(-6f, sprite ? -90f : -46f);   // 아이콘이 있으면 그 아래부터

                string why = run.CannotBuy(offer);
                tile.Buy.interactable = why == null;
                tile.PriceLabel.text = offer.Sold ? "품절" : $"{offer.Price} 골드";
                tile.BuyImage.color = offer.Sold ? new Color(0.3f, 0.3f, 0.3f, 1f) : why == null ? accent : new Color(0.55f, 0.5f, 0.4f, 1f);
                float a = offer.Sold ? 0.4f : 1f;
                tile.Title.color = new Color(1f, 1f, 1f, a);
                tile.Desc.color = new Color(1f, 1f, 1f, 0.8f * a);
            }
        }

        void BuyTile(int index)
        {
            var offer = _shopTiles[index].Offer;
            if (offer == null) return;
            var run = RunState.Current;
            var why = run.Buy(offer);
            _shopMessage.text = why ?? (offer.Kind == ShopOfferKind.Item
                ? $"{ShopPool.Title(offer)} 구매 (주운 아이템 칸에 들어감)"
                : $"{ShopPool.Title(offer)} 구매");
        }

        // ───────── 화면 만들기 ─────────

        void BuildShop()
        {
            // 왼쪽 위(파티 패널 위), 늘 펼쳐 둔다
            var panel = Panel(new Vector2(-480f, 200f), new Vector2(900f, 450f), "");
            panel.GetComponent<Image>().raycastTarget = true;
            _shopPanel = panel.gameObject;
            _shopTitle = panel.GetComponentInChildren<Text>();

            for (int i = 0; i < ShopTiles; i++)
            {
                int col = i % 5, row = i / 5;
                _shopTiles[i] = BuildShopTile(panel, i, new Vector2(-352f + col * 176f, 88f - row * 178f), new Vector2(170f, 172f));
            }

            _shopMessage = NewText(panel, "", 20);
            _shopMessage.color = accent;
            Place(_shopMessage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(860f, 26f));
        }

        ShopTile BuildShopTile(RectTransform parent, int index, Vector2 pos, Vector2 size)
        {
            var tile = new ShopTile();
            tile.Frame = NewImage(parent, Color.white);
            Place(tile.Frame.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            tile.Body = NewImage(tile.Frame.rectTransform, cardColor);
            var brt = tile.Body.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(4f, 4f); brt.offsetMax = new Vector2(-4f, -4f);

            tile.Kind = NewText(brt, "", 14);
            tile.Kind.color = accent;
            Band(tile.Kind.rectTransform, 1f, -4f, 18f);
            tile.Title = NewText(brt, "", 16);
            tile.Title.horizontalOverflow = HorizontalWrapMode.Wrap;
            Band(tile.Title.rectTransform, 1f, -22f, 22f);

            tile.Icon = NewImage(brt, Color.white);
            tile.Icon.preserveAspect = true;
            Place(tile.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(42f, 42f));
            tile.Icon.rectTransform.pivot = new Vector2(0.5f, 1f);

            tile.Desc = NewText(brt, "", 13);
            tile.Desc.alignment = TextAnchor.UpperCenter;
            tile.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var drt = tile.Desc.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(6f, 40f); drt.offsetMax = new Vector2(-6f, -46f);

            int i = index;
            tile.Buy = NewButton(brt, "", Vector2.zero, new Vector2(150f, 30f), accent, () => BuyTile(i));
            var bt = (RectTransform)tile.Buy.transform;
            bt.anchorMin = bt.anchorMax = bt.pivot = new Vector2(0.5f, 0f);
            bt.anchoredPosition = new Vector2(0f, 6f);
            tile.BuyImage = tile.Buy.GetComponent<Image>();
            tile.PriceLabel = tile.Buy.GetComponentInChildren<Text>();
            tile.Frame.gameObject.SetActive(false);
            return tile;
        }
    }
}
