using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>다음 던전 고르기: 스테이지를 깬 정비에서 나갈 때 뜬다. 1번은 바로, 2·3번은 경고창을 거쳐 들어간다.</summary>
    public sealed partial class CampHud
    {
        static readonly string[] DungeonWarnings = { "", "경고 · 전멸 위험", "경고 · 살아 돌아올 수 없음" };

        GameObject _dungeonBox, _dungeonConfirm;
        Text _dungeonTitle, _dungeonParty, _dungeonConfirmText;
        readonly (Text level, Text reward, Text drop, Text gems)[] _dungeonCards = new (Text, Text, Text, Text)[DungeonTier.Count];
        int _dungeonPick;

        static Color DungeonColor(int tier) => tier == 0 ? UiKit.Positive : tier == 1 ? UiKit.Negative : UiKit.Cursed;

        void BuildDungeon()
        {
            var dim = UiKit.NewImage(_root.transform, new Color(0f, 0f, 0f, 0.75f), raycast: true);   // 뒤를 못 누르게
            UiKit.Stretch(dim.rectTransform);
            _dungeonBox = dim.gameObject;
            var panel = UiKit.Panel(UiKit.NewImage(dim.transform, panelColor), rim: UiKit.Gold);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(1200f, 680f));
            var box = panel.transform;

            _dungeonTitle = UiKit.NewText(box, "", 38, UiKit.Gold);
            UiKit.Place(_dungeonTitle.rectTransform, new Vector2(0f, 300f), new Vector2(800f, 50f));
            _dungeonParty = UiKit.NewText(box, "", 24, Color.white);
            UiKit.Place(_dungeonParty.rectTransform, new Vector2(0f, 255f), new Vector2(800f, 34f));

            for (int i = 0; i < DungeonTier.Count; i++)
            {
                int tier = i;
                var rim = DungeonColor(i);
                var body = UiKit.NewImage(box, UiKit.CardFill, raycast: true, name: "Card");
                UiKit.Place(body.rectTransform, new Vector2((i - 1) * 385f, -20f), new Vector2(365f, 440f));
                var btn = body.gameObject.AddComponent<Button>();
                UiKit.Button(btn, body, UiKit.CardFill, rim: rim);
                btn.onClick.AddListener(() => PickDungeon(tier));
                var t = body.transform;

                var number = UiKit.NewText(t, (i + 1).ToString(), 44, rim);
                UiKit.Place(number.rectTransform, new Vector2(0f, 180f), new Vector2(100f, 54f));
                var name = UiKit.NewText(t, DungeonTier.Name(i), 32, i == 0 ? Color.white : rim);
                UiKit.Place(name.rectTransform, new Vector2(0f, 130f), new Vector2(320f, 40f));
                var caption = UiKit.NewText(t, "몬스터 평균", 20, new Color(1f, 1f, 1f, 0.7f));
                UiKit.Place(caption.rectTransform, new Vector2(0f, 82f), new Vector2(320f, 28f));
                var level = UiKit.NewText(t, "", 60, i == 0 ? Color.white : UiKit.Negative);
                UiKit.Place(level.rectTransform, new Vector2(0f, 32f), new Vector2(340f, 70f));
                var reward = UiKit.NewText(t, "", 22, UiKit.Gold);
                UiKit.Place(reward.rectTransform, new Vector2(0f, -35f), new Vector2(340f, 30f));
                var drop = UiKit.NewText(t, "", 22, UiKit.Gold);
                UiKit.Place(drop.rectTransform, new Vector2(0f, -68f), new Vector2(340f, 30f));
                var gems = UiKit.NewText(t, "", 22, UiKit.Gold);
                UiKit.Place(gems.rectTransform, new Vector2(0f, -101f), new Vector2(340f, 30f));
                _dungeonCards[i] = (level, reward, drop, gems);

                if (i == 0) continue;
                var strip = UiKit.NewImage(t, new Color(0.45f, 0.08f, 0.1f, 0.95f));
                UiKit.Place(strip.rectTransform, new Vector2(0f, -178f), new Vector2(345f, 56f));
                var warn = UiKit.NewText(strip.transform, DungeonWarnings[i], 24, new Color(1f, 0.85f, 0.8f, 1f));
                UiKit.Stretch(warn.rectTransform);
            }

            UiKit.NewButton(box, "돌아가기", new Vector2(0f, -295f), new Vector2(220f, 48f), new Color(0.6f, 0.62f, 0.7f, 1f), () => _dungeonBox.SetActive(false));

            // 2·3번 경고창
            var confirmDim = UiKit.NewImage(dim.transform, new Color(0f, 0f, 0f, 0.7f), raycast: true);
            UiKit.Stretch(confirmDim.rectTransform);
            _dungeonConfirm = confirmDim.gameObject;
            var confirm = UiKit.Panel(UiKit.NewImage(confirmDim.transform, panelColor), rim: UiKit.Negative);
            UiKit.Place(confirm.rectTransform, Vector2.zero, new Vector2(760f, 340f));
            var warnTitle = UiKit.NewText(confirm.transform, "경고", 44, UiKit.Negative);
            UiKit.Place(warnTitle.rectTransform, new Vector2(0f, 120f), new Vector2(400f, 56f));
            _dungeonConfirmText = UiKit.NewText(confirm.transform, "", 28, Color.white, TextAnchor.MiddleCenter, overflow: false);
            UiKit.Place(_dungeonConfirmText.rectTransform, new Vector2(0f, 15f), new Vector2(700f, 130f));
            UiKit.NewButton(confirm.transform, "입장", new Vector2(-130f, -115f), new Vector2(220f, 54f), UiKit.Negative, EnterDungeon);
            UiKit.NewButton(confirm.transform, "취소", new Vector2(130f, -115f), new Vector2(220f, 54f), new Color(0.6f, 0.62f, 0.7f, 1f), () => _dungeonConfirm.SetActive(false));
            _dungeonConfirm.SetActive(false);
            _dungeonBox.SetActive(false);
        }

        void OpenDungeonChoice()
        {
            var run = RunState.Current;
            int stage = run.Stage + 1, layers = run.Map.LayerCount;
            _dungeonTitle.text = $"스테이지 {stage} 입장";
            _dungeonParty.text = $"내 파티 평균 Lv.{run.AveragePartyLevel()}";
            for (int i = 0; i < DungeonTier.Count; i++)
            {
                var c = _dungeonCards[i];
                c.level.text = $"Lv.{DungeonTier.AverageLevel(stage, layers, i)}";
                c.reward.text = i == 0 ? "" : $"경험치·금화 ×{DungeonTier.RewardMul(stage, layers, i):0.#}";
                c.drop.text = i == 0 ? "" : $"드랍 ×{DungeonTier.DropMul(i):0.#}";
                c.gems.text = i == 0 ? "" : $"판 끝 잼 +{DungeonTier.EndGems(i)}";
            }
            _dungeonConfirm.SetActive(false);
            _dungeonBox.SetActive(true);
        }

        void PickDungeon(int tier)
        {
            _dungeonPick = tier;
            if (tier == 0) { EnterDungeon(); return; }
            var run = RunState.Current;
            int enemy = DungeonTier.AverageLevel(run.Stage + 1, run.Map.LayerCount, tier);
            _dungeonConfirmText.text = $"「{DungeonTier.Name(tier)}」 몬스터 평균 Lv.{enemy}\n내 파티 평균 Lv.{run.AveragePartyLevel()}\n\n입장하면 되돌릴 수 없습니다";
            _dungeonConfirm.SetActive(true);
        }

        void EnterDungeon()
        {
            _dungeonConfirm.SetActive(false);
            _dungeonBox.SetActive(false);
            RunState.Current.EnterNextStage(_dungeonPick);
        }
    }
}
