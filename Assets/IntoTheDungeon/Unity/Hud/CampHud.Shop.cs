using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 정비 화면의 상점: 왼쪽 위에 늘 펼쳐진 패널. 맨 위는 유물 진열대(금테 메달, 최대 2개), 그 아래 5칸 × 2줄에 아이템·영입·회복.
    /// 산 아이템은 주운 아이템 칸으로 들어가므로 끌어서 장비 칸·보관함에 넣는다. 팔기는 아이템을 "팔기" 칸에 놓는다.
    /// </summary>
    public sealed partial class CampHud
    {
        const int GridTiles = 10;   // 5열 × 2줄(아이템·영입·부활·회복)
        const int ShelfTiles = 2;   // 유물 진열대(상점 방 유물 최대 2개)
        const int ShopTiles = GridTiles + ShelfTiles;   // 진열대 칸은 뒤쪽 번호

        sealed class ShopTile
        {
            public Image Frame, Body, Icon;
            public Text Kind, Title, Desc, PriceLabel;
            public Button Buy;
            public Image BuyImage;
            public Image KindIcon;   // 왼쪽 위 작은 종류 아이콘(아이템·영입·회복·부활)
            public RelicMedallion Medal;   // 진열대 칸만
            public ShopOffer Offer;
        }

        readonly ShopTile[] _shopTiles = new ShopTile[ShopTiles];
        GameObject _shopPanel;
        Text _shopTitle, _shopMessage;

        void UpdateShop(RunData run)
        {
            _shopTitle.text = run.InShopRoom ? "상점  <size=20><color=#8CF08C>-20%</color></size>" : "떠돌이 상인";   // 골드는 오른쪽 위
            // 유물은 진열대 칸(뒤쪽 번호)에, 나머지는 격자에 차례로
            int grid = 0, shelf = GridTiles;
            for (int i = 0; i < _shopTiles.Length; i++) _shopTiles[i].Offer = null;
            foreach (var o in run.Shop)
            {
                if (o.Kind == ShopOfferKind.Card) { if (shelf < ShopTiles) _shopTiles[shelf++].Offer = o; }
                else if (grid < GridTiles) _shopTiles[grid++].Offer = o;
            }
            for (int i = 0; i < _shopTiles.Length; i++)
            {
                var tile = _shopTiles[i];
                var offer = tile.Offer;
                if (tile.Frame.gameObject.activeSelf != (offer != null)) tile.Frame.gameObject.SetActive(offer != null);
                if (offer == null) continue;

                bool item = offer.Kind == ShopOfferKind.Item;
                var rim = item ? LootRenderer.RarityColor(offer.Item.Rarity)
                        : offer.Kind == ShopOfferKind.Recruit ? recruitColor
                        : offer.Kind == ShopOfferKind.Card ? LootRenderer.RarityColor(offer.Card.Rarity)   // 유물: 등급 색
                        : UiKit.RimDim;
                UiKit.SetRim(tile.Body, offer.Kind == ShopOfferKind.Card ? UiKit.Rim : rim);   // 유물은 금테(등급은 메달 보석 점)
                if (tile.Medal != null)
                {
                    if (tile.Medal.Relic != offer.Card) tile.Medal.Set(offer.Card);
                    tile.Title.text = offer.Card.Title;
                    tile.Desc.text = StopPowerUi.Highlight(offer.Card.Description);
                    string whyShelf = run.CannotBuy(offer);
                    tile.Buy.interactable = whyShelf == null;
                    tile.PriceLabel.text = offer.Sold ? "품절" : $"     {offer.Price}";
                    tile.BuyImage.color = offer.Sold ? new Color(0.3f, 0.3f, 0.3f, 1f) : whyShelf == null ? accent : new Color(0.55f, 0.5f, 0.4f, 1f);
                    float sa = offer.Sold ? 0.4f : 1f;
                    tile.Title.color = new Color(1f, 0.9f, 0.65f, sa);
                    tile.Desc.color = new Color(1f, 1f, 1f, 0.75f * sa);
                    continue;
                }
                tile.Kind.text = "";
                tile.KindIcon.sprite = UiIcons.Of(offer.Kind switch
                {
                    ShopOfferKind.Item => UiIcons.Item,
                    ShopOfferKind.Card => UiIcons.Relic,
                    ShopOfferKind.Recruit => UiIcons.Recruit,
                    ShopOfferKind.Revive => UiIcons.Revive,
                    _ => UiIcons.Heal
                });
                tile.Title.text = ShopPool.Title(offer);
                tile.Desc.text = "";   // 설명은 마우스를 올리면 아래 안내 줄에
                // 가운데 큰 그림: 아이템·유물 아이콘, 영입은 초상화, 회복·부활은 그 아이콘
                var sprite = item ? ItemIconTable.Icon(offer.Item.Id)
                           : offer.Kind == ShopOfferKind.Card ? RelicIcons.Of(offer.Card.Id)
                           : offer.Kind == ShopOfferKind.Recruit ? PortraitTable.Of(offer.Card.RecruitRecipe)
                           : UiIcons.Of(offer.Kind == ShopOfferKind.Revive ? UiIcons.Revive : UiIcons.Heal);
                tile.Icon.sprite = sprite;
                tile.Icon.enabled = sprite;

                string why = run.CannotBuy(offer);
                tile.Buy.interactable = why == null;
                tile.PriceLabel.text = offer.Sold ? "품절" : $"     {offer.Price}";   // 앞은 동전 아이콘 자리
                tile.BuyImage.color = offer.Sold ? new Color(0.3f, 0.3f, 0.3f, 1f) : why == null ? accent : new Color(0.55f, 0.5f, 0.4f, 1f);
                float a = offer.Sold ? 0.4f : 1f;
                tile.Title.color = new Color(1f, 1f, 1f, a);
                tile.Desc.color = new Color(1f, 1f, 1f, 0.8f * a);
            }
        }

        void HoverTile(int index)
        {
            var offer = _shopTiles[index].Offer;
            if (offer == null) return;
            _shopMessage.text = $"{ShopPool.KindLabel(offer)} · {ShopPool.Title(offer)}:  {StopPowerUi.Highlight(ShopPool.Description(offer).Replace("\n", " "))}";
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

            // 유물 진열대(맨 위 줄): 어두운 받침 위에 금테 메달
            var shelfBack = UiKit.Card(NewImage(panel, Color.white), new Color(0.78f, 0.62f, 0.36f, 0.6f), new Color(0.12f, 0.09f, 0.06f, 0.9f));
            Place(shelfBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 122f), new Vector2(870f, 100f));
            var shelfLabel = UiKit.Soft(NewText(shelfBack.rectTransform, "유물", 18));
            shelfLabel.color = UiKit.Gold;
            Place(shelfLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(50f, 30f));
            for (int i = 0; i < ShelfTiles; i++)
                _shopTiles[GridTiles + i] = BuildShelfTile(panel, GridTiles + i, new Vector2(-188f + i * 430f, 122f), new Vector2(410f, 88f));
            for (int i = 0; i < GridTiles; i++)
            {
                int col = i % 5, row = i / 5;
                _shopTiles[i] = BuildShopTile(panel, i, new Vector2(-352f + col * 176f, 6f - row * 134f), new Vector2(170f, 128f));
            }

            _shopMessage = NewText(panel, "", 20);
            _shopMessage.color = accent;
            Place(_shopMessage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(860f, 26f));
        }

        ShopTile BuildShopTile(RectTransform parent, int index, Vector2 pos, Vector2 size)
        {
            var tile = new ShopTile();
            tile.Frame = NewImage(parent, new Color(0f, 0f, 0f, 0f));   // 자리만(테두리는 Body의 둥근 테)
            Place(tile.Frame.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            tile.Body = UiKit.Card(NewImage(tile.Frame.rectTransform, cardColor));
            var brt = tile.Body.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(2f, 2f); brt.offsetMax = new Vector2(-2f, -2f);
            tile.KindIcon = NewImage(brt, Color.white);
            var kirt = tile.KindIcon.rectTransform;
            kirt.anchorMin = kirt.anchorMax = kirt.pivot = new Vector2(0f, 1f);
            kirt.anchoredPosition = new Vector2(6f, -6f);
            kirt.sizeDelta = new Vector2(22f, 22f);
            int hoverIndex = index;
            HoverHook.On(tile.Body.gameObject, () => HoverTile(hoverIndex), () => _shopMessage.text = "");

            tile.Kind = NewText(brt, "", 14);
            tile.Kind.color = accent;
            Band(tile.Kind.rectTransform, 1f, -4f, 18f);
            tile.Title = NewText(brt, "", 16);
            tile.Title.horizontalOverflow = HorizontalWrapMode.Wrap;
            Band(tile.Title.rectTransform, 1f, -22f, 22f);

            tile.Icon = NewImage(brt, Color.white);
            tile.Icon.preserveAspect = true;
            Place(tile.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(50f, 50f));
            tile.Icon.rectTransform.pivot = new Vector2(0.5f, 1f);

            tile.Desc = NewText(brt, "", 13);
            tile.Desc.alignment = TextAnchor.UpperCenter;
            tile.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var drt = tile.Desc.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(6f, 40f); drt.offsetMax = new Vector2(-6f, -46f);

            int i = index;
            tile.Buy = NewButton(brt, "", Vector2.zero, new Vector2(150f, 28f), accent, () => BuyTile(i));
            var bt = (RectTransform)tile.Buy.transform;
            bt.anchorMin = bt.anchorMax = bt.pivot = new Vector2(0.5f, 0f);
            bt.anchoredPosition = new Vector2(0f, 5f);
            tile.BuyImage = tile.Buy.GetComponent<Image>();
            tile.PriceLabel = tile.Buy.GetComponentInChildren<Text>();
            var coin = NewImage(tile.Buy.transform, Color.white);   // 가격 앞 동전
            coin.sprite = RelicIcons.Of("coin");
            var crt = coin.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-8f, 0f);
            crt.sizeDelta = new Vector2(22f, 22f);
            tile.Frame.gameObject.SetActive(false);
            return tile;
        }

        // 유물 진열대 칸: 왼쪽 메달 · 가운데 이름·효과 · 오른쪽 아래 가격
        ShopTile BuildShelfTile(RectTransform parent, int index, Vector2 pos, Vector2 size)
        {
            var tile = BuildShopTile(parent, index, pos, size);
            var brt = tile.Body.rectTransform;
            tile.KindIcon.enabled = false;
            tile.Icon.enabled = false;
            tile.Icon.gameObject.SetActive(false);
            tile.Medal = RelicMedallion.Build(brt, 66f);
            var mrt = (RectTransform)tile.Medal.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0f, 0.5f);
            mrt.anchoredPosition = new Vector2(46f, 6f);
            var trt = tile.Title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0f, 1f);
            trt.offsetMin = new Vector2(92f, -34f); trt.offsetMax = new Vector2(-10f, -8f);
            tile.Title.alignment = TextAnchor.MiddleLeft;
            tile.Title.fontSize = 19;
            var drt = tile.Desc.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(92f, 6f); drt.offsetMax = new Vector2(-150f, -36f);
            tile.Desc.alignment = TextAnchor.UpperLeft;
            tile.Desc.fontSize = 15;
            var bt = (RectTransform)tile.Buy.transform;
            bt.anchorMin = bt.anchorMax = bt.pivot = new Vector2(1f, 0f);
            bt.anchoredPosition = new Vector2(-8f, 8f);
            bt.sizeDelta = new Vector2(130f, 30f);
            return tile;
        }
    }
}
